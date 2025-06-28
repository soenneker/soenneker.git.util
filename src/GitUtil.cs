using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using Soenneker.Extensions.Configuration;
using Soenneker.Extensions.Task;
using Soenneker.Extensions.ValueTask;
using Soenneker.Git.Util.Abstract;
using Soenneker.Utils.Directory.Abstract;
using Soenneker.Utils.Path.Abstract;
using Soenneker.Utils.Process.Abstract;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Git.Util;

///<inheritdoc crGetAllGitRepositoriesRecursivelyef="IGitUtil"/>
public sealed class GitUtil : IGitUtil
{
    // ▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬  Read‑only config snapshot  ▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬
    private readonly string _configToken;
    private readonly string _configName;
    private readonly string _configEmail;
    private readonly string _defaultBranch;
    private readonly bool _logGitCommands;

    // ▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬  Services  ▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬
    private readonly ILogger<GitUtil> _logger;
    private readonly IDirectoryUtil _directoryUtil;
    private readonly IProcessUtil _processUtil;
    private readonly IPathUtil _pathUtil;

    // ▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬  Other fields  ▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬
    private readonly string _gitBinaryPath;
    private readonly AsyncRetryPolicy _retry429;

    public GitUtil(IConfiguration config, ILogger<GitUtil> logger, IDirectoryUtil directoryUtil, IProcessUtil processUtil, IPathUtil pathUtil)
    {
        _logger = logger;
        _directoryUtil = directoryUtil;
        _processUtil = processUtil;
        _pathUtil = pathUtil;

        // Capture config once – avoids mid‑run reload surprises
        _configToken = config.GetValueStrict<string>("Git:Token");
        _configName = config.GetValueStrict<string>("Git:Name");
        _configEmail = config.GetValueStrict<string>("Git:Email");
        _defaultBranch = config.GetValue<string>("Git:DefaultBranch") ?? "main";
        _logGitCommands = config.GetValue<bool>("Git:Log");

        _gitBinaryPath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Path.Join(AppContext.BaseDirectory, "Resources", "win-x64", "git", "cmd", "git.exe")
            : Path.Join(AppContext.BaseDirectory, "Resources", "linux-x64", "git", "bin", "git");

        // NOTE: HttpRequestException.StatusCode is available on .NET 6+. Predicate is guarded accordingly.
        _retry429 = Policy.Handle<HttpRequestException>(static ex =>
                          {
                              var prop = typeof(HttpRequestException).GetProperty("StatusCode");
                              return prop?.GetValue(ex) is HttpStatusCode code && code == HttpStatusCode.TooManyRequests;
                          })
                          .WaitAndRetryAsync(5, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)) + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 500)),
                              (ex, ts, attempt, _) => logger.LogWarning("429 detected – retry #{Attempt} in {Delay:n1}s", attempt, ts.TotalSeconds));
    }

    private static ParallelOptions CreateParallelOptions(CancellationToken cancellationToken)
    {
        return new ParallelOptions
        {
            // Cap to 8 threads – enough for network‑bound Git without overwhelming disks
            MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, 8),
            CancellationToken = cancellationToken
        };
    }

    private string BuildAuthHeader(string? token = null)
    {
        token ??= _configToken;
        string basic = Convert.ToBase64String(Encoding.ASCII.GetBytes(":" + token)); // empty user name + PAT
        return $"Authorization: Basic {basic}";
    }

    private static bool LooksLikeRepo(string dir)
    {
        // Fast file‑system check – avoids spawning Git just for discovery
        string gitDir = Path.Join(dir, ".git");
        return Directory.Exists(gitDir) || File.Exists(gitDir);
    }

    /// <summary>
    /// Fast deep scan using FileSystemEnumerable. Returns the root path of every Git repository found.
    /// </summary>
    private static IEnumerable<string> EnumerateRepoRoots(string root)
    {
        var opts = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            AttributesToSkip = 0, // don’t filter hidden/system
            IgnoreInaccessible = true
        };

        var enumerable = new FileSystemEnumerable<string>(root, (ref FileSystemEntry entry) => Path.GetDirectoryName(entry.ToFullPath())!, opts)
        {
            ShouldIncludePredicate = static (ref FileSystemEntry entry) => entry.IsDirectory && entry.FileName.Equals(".git", StringComparison.OrdinalIgnoreCase)
        };

        foreach (string repoRootWithGit in enumerable)
        {
            // Trim the trailing "\.git"
            yield return Path.GetDirectoryName(repoRootWithGit)!;
        }
    }

    public async ValueTask<List<string>> Run(string arguments, string? workingDirectory = null, Dictionary<string, string>? env = null, bool log = true, CancellationToken cancellationToken = default)
    {
        if (_logGitCommands)
        {
            _logger.LogInformation("[git] {GitBinary} {Arguments} (cwd: {Cwd})", _gitBinaryPath, arguments, workingDirectory ?? "<null>");
        }

        env ??= new Dictionary<string, string>();
        env["GIT_TERMINAL_PROMPT"] = "0"; // Disable terminal prompts for credentials

        return await _processUtil.Start(_gitBinaryPath, workingDirectory, arguments, environmentalVars: env, log: log,
                                     cancellationToken: cancellationToken)
                                 .NoSync();
    }

    private static async Task ForEachRepo(List<string> repos, bool parallel, CancellationToken ct, Func<string, CancellationToken, ValueTask> action)
    {
        if (parallel)
        {
            await Parallel.ForEachAsync(repos, CreateParallelOptions(ct), async (repo, token) => { await action(repo, token).NoSync(); });
        }
        else
        {
            foreach (string repo in repos)
            {
                ct.ThrowIfCancellationRequested();
                await action(repo, ct).NoSync();
            }
        }
    }


    public async ValueTask PullAllGitRepositories(string root, string? token = null, bool parallel = false, CancellationToken cancellationToken = default)
    {
        List<string> repos = GetAllGitRepositoriesRecursively(root);
        await ForEachRepo(repos, parallel, cancellationToken, (repo, ct) => Pull(repo, token, ct)).NoSync();
    }

    public async ValueTask FetchAllGitRepositories(string root, string? token = null, bool parallel = false, CancellationToken cancellationToken = default)
    {
        List<string> repos = GetAllGitRepositoriesRecursively(root);
        await ForEachRepo(repos, parallel, cancellationToken, (repo, ct) => Fetch(repo, token, ct)).NoSync();
    }

    public async ValueTask SwitchAllGitRepositoriesToRemoteBranch(string root, string? token = null, bool parallel = false,
        CancellationToken cancellationToken = default)
    {
        List<string> repos = GetAllGitRepositoriesRecursively(root);
        await ForEachRepo(repos, parallel, cancellationToken, (repo, ct) => SwitchToRemoteBranch(repo, token, ct)).NoSync();
    }

    public async ValueTask CommitAllRepositories(string root, string commitMessage, bool parallel = false, CancellationToken ct = default)
    {
        List<string> repos = GetAllGitRepositoriesRecursively(root);
        await ForEachRepo(repos, parallel, ct, (repo, cancellationToken) => Commit(repo, commitMessage, null, null, cancellationToken)).NoSync();
    }

    public async ValueTask PushAllRepositories(string root, string token, bool parallel = false, CancellationToken ct = default)
    {
        List<string> repos = GetAllGitRepositoriesRecursively(root);
        await ForEachRepo(repos, parallel, ct, (repo, cancellationToken) => Push(repo, token, cancellationToken)).NoSync();
    }

    public async ValueTask SwitchToRemoteBranch(string directory, string? token = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var env = new Dictionary<string, string> {["GIT_HTTP_EXTRAHEADER"] = BuildAuthHeader(token)};
            await Run("fetch origin", directory, env: env, cancellationToken: cancellationToken).NoSync();
            await Run($"checkout {_defaultBranch}", directory, cancellationToken: cancellationToken).NoSync();
            await Run($"reset --hard origin/{_defaultBranch}", directory, cancellationToken: cancellationToken).NoSync();
            _logger.LogInformation("Switched {Dir} to remote branch '{Branch}'", directory, _defaultBranch);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not switch to remote branch for {Dir}", directory);
        }
    }

    public async ValueTask<bool> IsRepositoryDirty(string directory, CancellationToken cancellationToken = default)
    {
        try
        {
            // Fast timestamp heuristic – bail early if nothing changed.
            string gitDir = Path.Join(directory, ".git");
            string indexFile = Path.Join(gitDir, "index");
            string headFile = Path.Join(gitDir, "HEAD");

            if (File.Exists(indexFile) && File.Exists(headFile))
            {
                DateTime indexTime = File.GetLastWriteTimeUtc(indexFile);
                DateTime headTime = File.GetLastWriteTimeUtc(headFile);
                if (indexTime <= headTime)
                    return false; // likely clean
            }

            List<string> status = await Run("status --porcelain", directory, cancellationToken: cancellationToken).NoSync();
            return status.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    public async ValueTask<bool> IsRepository(string directory, CancellationToken cancellationToken = default)
    {
        // keep public API; internally we use LooksLikeRepo for speed
        try
        {
            List<string> output = await Run("rev-parse --is-inside-work-tree", directory, log: false, cancellationToken: cancellationToken).NoSync();
            return output.Count > 0 && output[0].Trim() == "true";
        }
        catch
        {
            return false;
        }
    }

    public async ValueTask Clone(string uri, string directory, string? token = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Cloning {Uri} into {Dir} ...", uri, directory);

        try
        {
            var env = new Dictionary<string, string> {["GIT_HTTP_EXTRAHEADER"] = BuildAuthHeader(token)};
            await Run($"clone --filter=blob:none --depth=1 \"{uri}\" \"{directory}\"", null, env: env, cancellationToken: cancellationToken).NoSync();
            _logger.LogInformation("Finished cloning {Uri}", uri);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not clone {Uri} into {Dir}", uri, directory);
            throw;
        }
    }

    public async ValueTask<string> CloneToTempDirectory(string uri, string? token = null, CancellationToken cancellationToken = default)
    {
        string dir = await _directoryUtil.CreateTempDirectory(cancellationToken).NoSync();

        try
        {
            await Clone(uri, dir, token, cancellationToken).NoSync();
            return dir;
        }
        catch
        {
            // Clean up temp directory if clone fails
            try
            {
                _directoryUtil.Delete(dir);
            }
            catch
            {
                /* ignored */
            }

            throw;
        }
    }

    public async ValueTask Pull(string directory, string? token = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var env = new Dictionary<string, string> {["GIT_HTTP_EXTRAHEADER"] = BuildAuthHeader(token)};
            await Run($"pull --ff-only origin {_defaultBranch}", directory, env: env, cancellationToken: cancellationToken).NoSync();
            _logger.LogDebug("Pulled latest changes for {Dir}", directory);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Merge conflict when pulling {Dir}", directory);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not pull in {Dir}", directory);
        }
    }

    public async ValueTask Commit(string directory, string message, string? name = null, string? email = null, CancellationToken cancellationToken = default)
    {
        name ??= _configName;
        email ??= _configEmail;

        try
        {
            if (!await IsRepositoryDirty(directory, cancellationToken).NoSync())
            {
                _logger.LogInformation("No changes detected in {Dir}", directory);
                return;
            }

            var env = new Dictionary<string, string>
            {
                ["GIT_AUTHOR_NAME"] = name,
                ["GIT_AUTHOR_EMAIL"] = email,
                ["GIT_COMMITTER_NAME"] = name,
                ["GIT_COMMITTER_EMAIL"] = email
            };

            await Run("add -A", directory, cancellationToken: cancellationToken).NoSync();

            string msgFile = await _pathUtil.GetRandomTempFilePath(".tmp", cancellationToken);
            await File.WriteAllTextAsync(msgFile, message, cancellationToken).NoSync();

            try
            {
                await Run($"commit -F \"{msgFile}\"", directory, env: env, cancellationToken: cancellationToken).NoSync();
            }
            finally
            {
                try
                {
                    File.Delete(msgFile);
                }
                catch
                {
                    /* ignored */
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not commit in {Dir}", directory);
        }
    }

    public async ValueTask Push(string directory, string token, CancellationToken cancellationToken = default)
    {
        try
        {
            string remoteUrl = await GetRemoteUrl(directory, cancellationToken).NoSync();
            if (string.IsNullOrEmpty(remoteUrl))
                throw new InvalidOperationException($"Could not get remote URL for repository in {directory}.");

            UriBuilder ub = new(remoteUrl) {UserName = token};
            string authenticatedUrl = ub.ToString();

            var pushCommand = $"push \"{authenticatedUrl}\" HEAD:{_defaultBranch}";

            await _retry429.ExecuteAsync(async () => { await Run(pushCommand, directory, cancellationToken: cancellationToken).NoSync(); }).NoSync();

            _logger.LogInformation("Successfully pushed to {Dir}", directory);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not push in {Dir}", directory);
        }
    }

    private async ValueTask<string> GetRemoteUrl(string directory, CancellationToken cancellationToken)
    {
        try
        {
            List<string> output = await Run("remote get-url origin", directory, log: false, cancellationToken: cancellationToken).NoSync();
            if (output.Count > 0 && !string.IsNullOrWhiteSpace(output[0]))
                return output[0].Trim();

            _logger.LogWarning("Could not determine remote URL for 'origin' in directory {Dir}. Push will likely fail.", directory);
            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get remote URL for {Dir}", directory);
            return string.Empty;
        }
    }

    public async ValueTask AddIfNotExists(string directory, string relativeFilePath, CancellationToken cancellationToken = default)
    {
        try
        {
            bool alreadyTracked = (await Run($"ls-files --error-unmatch \"{relativeFilePath}\"", directory, cancellationToken: cancellationToken)
                .NoSync()).Count > 0;
            if (!alreadyTracked)
                await Run($"add \"{relativeFilePath}\"", directory, cancellationToken: cancellationToken).NoSync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not add {File} in {Dir}", relativeFilePath, directory);
            throw;
        }
    }

    public async ValueTask Fetch(string directory, string? token = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var env = new Dictionary<string, string> {["GIT_HTTP_EXTRAHEADER"] = BuildAuthHeader(token)};
            await Run("fetch origin --filter=blob:none --prune", directory, env: env, cancellationToken: cancellationToken).NoSync();
            _logger.LogInformation("Fetched {Dir}", directory);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not fetch in {Dir}", directory);
        }
    }


    public List<string> GetAllGitRepositoriesRecursively(string directory)
    {
        _logger.LogDebug("Scanning for git repositories under {Root}", directory);
        return EnumerateRepoRoots(directory).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async ValueTask<List<string>> GetAllDirtyRepositories(string directory, CancellationToken cancellationToken = default)
    {
        List<string> repos = GetAllGitRepositoriesRecursively(directory);
        var dirty = new ConcurrentBag<string>();

        await Parallel.ForEachAsync(repos, CreateParallelOptions(cancellationToken), async (repo, ct) =>
                      {
                          if (await IsRepositoryDirty(repo, ct).NoSync())
                              dirty.Add(repo);
                      })
                      .NoSync();

        return dirty.ToList();
    }


    public async ValueTask CommitAndPush(string directory, string message, string token, string? name = null, string? email = null,
        CancellationToken ct = default)
    {
        if (!await IsRepositoryDirty(directory, ct).NoSync())
        {
            _logger.LogInformation("No changes to commit in {Dir}", directory);
            return;
        }

        await Commit(directory, message, name, email, ct).NoSync();
        await Push(directory, token, ct).NoSync();
    }
}