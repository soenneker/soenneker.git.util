using System.Linq;
using Soenneker.Extensions.Configuration;
using Soenneker.Extensions.Task;
using Soenneker.Extensions.ValueTask;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using Soenneker.Git.Util.Abstract;
using Soenneker.Utils.Directory.Abstract;
using Soenneker.Utils.Path.Abstract;
using Soenneker.Utils.Process.Abstract;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Soenneker.Utils.Random;

namespace Soenneker.Git.Util;

/// <inheritdoc cref="IGitUtil"/>
public sealed class GitUtil : IGitUtil
{
    // ▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬  Read-only config snapshot  ▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬
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

    // Cache Authorization header per token to avoid base64 work every call
    private readonly ConcurrentDictionary<string, string> _authHeaderCache = new(StringComparer.Ordinal);

    private const string _gitTerminalPromptKey = "GIT_TERMINAL_PROMPT";
    private const string _gitTerminalPromptValue = "0";

    public GitUtil(IConfiguration config, ILogger<GitUtil> logger, IDirectoryUtil directoryUtil, IProcessUtil processUtil, IPathUtil pathUtil)
    {
        _logger = logger;
        _directoryUtil = directoryUtil;
        _processUtil = processUtil;
        _pathUtil = pathUtil;

        // Capture config once – avoids mid-run reload surprises
        _configToken = config.GetValueStrict<string>("Git:Token");
        _configName = config.GetValueStrict<string>("Git:Name");
        _configEmail = config.GetValueStrict<string>("Git:Email");
        _defaultBranch = config.GetValue<string>("Git:DefaultBranch") ?? "main";
        _logGitCommands = config.GetValue<bool>("Git:Log");

        _gitBinaryPath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Path.Join(AppContext.BaseDirectory, "Resources", "win-x64", "git", "cmd", "git.exe")
            : Path.Join(AppContext.BaseDirectory, "Resources", "linux-x64", "git", "git.sh");

        // .NET 10: HttpRequestException.StatusCode exists, no reflection required.
        _retry429 = Policy.Handle<HttpRequestException>(static ex => ex.StatusCode == HttpStatusCode.TooManyRequests)
                          .WaitAndRetryAsync(retryCount: 5, sleepDurationProvider: static attempt =>
                                  // cheaper than Math.Pow(2, attempt)
                                  TimeSpan.FromSeconds(1 << attempt) + TimeSpan.FromMilliseconds(RandomUtil.Next(0, 500)),
                              onRetry: (ex, ts, attempt, _) => logger.LogWarning("429 detected – retry #{Attempt} in {Delay:n1}s", attempt, ts.TotalSeconds));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ParallelOptions CreateParallelOptions(CancellationToken cancellationToken)
    {
        return new ParallelOptions
        {
            // Cap to 8 threads – enough for network-bound Git without overwhelming disks
            MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, 8),
            CancellationToken = cancellationToken
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string BuildAuthHeaderCached(string? token = null)
    {
        token ??= _configToken ?? throw new InvalidOperationException("A token is required but none was provided.");

        return _authHeaderCache.GetOrAdd(token, static t =>
        {
            // GitHub docs recommend "x-access-token", but any non-empty string works
            const string user = "x-access-token";

            // Avoid interpolation
            string userColonToken = string.Concat(user, ":", t);

            // Base64 is unavoidable unless you implement span-based Base64 encoding; caching makes it amortized.
            string basic = Convert.ToBase64String(Encoding.ASCII.GetBytes(userColonToken));

            return string.Concat("Authorization: Basic ", basic);
        });
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Dictionary<string, string> CreateAuthEnv(string? token)
    {
        // Pre-size to avoid resize; Run() will TryAdd prompt too, but we include it here to keep
        // environments self-contained and avoid mutating a shared dictionary elsewhere.
        return new Dictionary<string, string>(capacity: 2)
        {
            ["GIT_HTTP_EXTRAHEADER"] = BuildAuthHeaderCached(token),
            [_gitTerminalPromptKey] = _gitTerminalPromptValue
        };
    }

    public async ValueTask<List<string>> Run(string arguments, string? workingDirectory = null, Dictionary<string, string>? env = null, bool log = true,
        CancellationToken cancellationToken = default)
    {
        if (_logGitCommands && log)
            _logger.LogInformation("[git] {GitBinary} {Arguments} (cwd: {Cwd})", _gitBinaryPath, arguments, workingDirectory ?? "<null>");

        // Avoid allocating a dictionary if caller didn't need one.
        if (env is null)
        {
            env = new Dictionary<string, string>(capacity: 1)
            {
                [_gitTerminalPromptKey] = _gitTerminalPromptValue
            };
        }
        else
        {
            // Don't overwrite caller value
            env.TryAdd(_gitTerminalPromptKey, _gitTerminalPromptValue);
        }

        return await _processUtil.Start(_gitBinaryPath, workingDirectory, arguments, environmentalVars: env, log: log, cancellationToken: cancellationToken)
                                 .NoSync();
    }

    private static async Task ForEachRepo(List<string> repos, bool parallel, CancellationToken ct, Func<string, CancellationToken, ValueTask> action)
    {
        if (parallel)
        {
            await Parallel.ForEachAsync(repos, CreateParallelOptions(ct), async (repo, token) =>
            {
                await action(repo, token)
                    .NoSync();
            });
        }
        else
        {
            foreach (string repo in repos)
            {
                ct.ThrowIfCancellationRequested();
                await action(repo, ct)
                    .NoSync();
            }
        }
    }

    public async ValueTask PullAllGitRepositories(string root, string? token = null, bool parallel = false, CancellationToken cancellationToken = default)
    {
        List<string> repos = GetAllGitRepositoriesRecursively(root);
        await ForEachRepo(repos, parallel, cancellationToken, (repo, ct) => Pull(repo, token, ct))
            .NoSync();
    }

    public async ValueTask FetchAllGitRepositories(string root, string? token = null, bool parallel = false, CancellationToken cancellationToken = default)
    {
        List<string> repos = GetAllGitRepositoriesRecursively(root);
        await ForEachRepo(repos, parallel, cancellationToken, (repo, ct) => Fetch(repo, token, ct))
            .NoSync();
    }

    public async ValueTask SwitchAllGitRepositoriesToRemoteBranch(string root, string? token = null, bool parallel = false,
        CancellationToken cancellationToken = default)
    {
        List<string> repos = GetAllGitRepositoriesRecursively(root);
        await ForEachRepo(repos, parallel, cancellationToken, (repo, ct) => SwitchToRemoteBranch(repo, token, ct))
            .NoSync();
    }

    public async ValueTask CommitAllRepositories(string root, string commitMessage, bool parallel = false, CancellationToken ct = default)
    {
        List<string> repos = GetAllGitRepositoriesRecursively(root);
        await ForEachRepo(repos, parallel, ct, (repo, cancellationToken) => Commit(repo, commitMessage, null, null, cancellationToken))
            .NoSync();
    }

    public async ValueTask PushAllRepositories(string root, string token, bool parallel = false, CancellationToken ct = default)
    {
        List<string> repos = GetAllGitRepositoriesRecursively(root);
        await ForEachRepo(repos, parallel, ct, (repo, cancellationToken) => Push(repo, token, cancellationToken))
            .NoSync();
    }

    public async ValueTask PullAndPushAllRepositories(string root, string token, bool parallel = false, CancellationToken cancellationToken = default)
    {
        List<string> repos = GetAllGitRepositoriesRecursively(root);
        await ForEachRepo(repos, parallel, cancellationToken, async (repo, ct) =>
            {
                await Pull(repo, token, ct)
                    .NoSync();
                await Push(repo, token, ct)
                    .NoSync();
            })
            .NoSync();
    }

    public async ValueTask SwitchToRemoteBranch(string directory, string? token = null, CancellationToken cancellationToken = default)
    {
        try
        {
            Dictionary<string, string> env = CreateAuthEnv(token);

            await Run("fetch origin", directory, env: env, cancellationToken: cancellationToken)
                .NoSync();
            await Run($"checkout {_defaultBranch}", directory, cancellationToken: cancellationToken)
                .NoSync();
            await Run($"reset --hard origin/{_defaultBranch}", directory, cancellationToken: cancellationToken)
                .NoSync();

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
            // Local working-tree changes?
            if ((await Run("status --porcelain", directory, log: false, cancellationToken: cancellationToken)
                    .NoSync()).Count > 0)
                return true;

            // Remote ahead/behind check (treat “no upstream” as “no divergence”)
            return await HasRemoteDiverged(directory, cancellationToken)
                .NoSync();
        }
        catch
        {
            // Play it safe – assume clean on error (matches your existing behavior).
            return false;
        }
    }

    private async ValueTask<bool> HasRemoteDiverged(string directory, CancellationToken ct)
    {
        try
        {
            List<string> lines = await Run("rev-list --left-right --count @{u}...HEAD", directory, log: false, cancellationToken: ct)
                .NoSync();
            if (lines.Count == 0)
                return false;

            ReadOnlySpan<char> s = lines[0]
                                   .AsSpan()
                                   .Trim();

            // common clean case: "0\t0"
            if (s.SequenceEqual("0\t0".AsSpan()))
                return false;

            int tab = s.IndexOf('\t');
            if (tab <= 0 || tab >= s.Length - 1)
                return false;

            ReadOnlySpan<char> left = s[..tab]
                .Trim();
            ReadOnlySpan<char> right = s[(tab + 1)..]
                .Trim();

            return !(left.SequenceEqual("0".AsSpan()) && right.SequenceEqual("0".AsSpan()));
        }
        catch
        {
            return false;
        }
    }

    public async ValueTask<bool> IsRepository(string directory, CancellationToken cancellationToken = default)
    {
        try
        {
            List<string> output = await Run("rev-parse --is-inside-work-tree", directory, log: false, cancellationToken: cancellationToken)
                .NoSync();
            return output.Count > 0 && output[0]
                                       .Trim()
                                       .Equals("true", StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    public async ValueTask Clone(string uri, string directory, string? token = null, bool shallow = false, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Cloning {Uri} into {Dir} ...", uri, directory);

        try
        {
            Dictionary<string, string> env = CreateAuthEnv(token);

            string depthArg = shallow ? "--filter=blob:none --depth=1" : string.Empty;
            var args = $"clone {depthArg} \"{uri}\" \"{directory}\"";

            await Run(args, env: env, cancellationToken: cancellationToken)
                .NoSync();
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
        string dir = await _directoryUtil.CreateTempDirectory(cancellationToken)
                                         .NoSync();

        try
        {
            await Clone(uri, dir, token, true, cancellationToken)
                .NoSync();
            return dir;
        }
        catch
        {
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
            Dictionary<string, string> env = CreateAuthEnv(token);

            await Run($"pull origin {_defaultBranch}", directory, env: env, cancellationToken: cancellationToken)
                .NoSync();
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
            if (!await IsRepositoryDirty(directory, cancellationToken)
                    .NoSync())
            {
                _logger.LogInformation("No changes detected in {Dir}", directory);
                return;
            }

            var env = new Dictionary<string, string>(capacity: 4)
            {
                ["GIT_AUTHOR_NAME"] = name,
                ["GIT_AUTHOR_EMAIL"] = email,
                ["GIT_COMMITTER_NAME"] = name,
                ["GIT_COMMITTER_EMAIL"] = email
            };

            await Run("add -A", directory, cancellationToken: cancellationToken)
                .NoSync();

            string msgFile = await _pathUtil.GetRandomTempFilePath(".tmp", cancellationToken)
                                            .NoSync();
            await File.WriteAllTextAsync(msgFile, message, cancellationToken)
                      .NoSync();

            try
            {
                await Run($"commit -F \"{msgFile}\"", directory, env: env, cancellationToken: cancellationToken)
                    .NoSync();
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
            // 1. Get and sanity-check remote URL
            string remoteUrl = await GetRemoteUrl(directory, cancellationToken)
                .NoSync();
            if (string.IsNullOrWhiteSpace(remoteUrl) || !remoteUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Cannot push from {directory}. Remote 'origin' URL is missing or not HTTPS (was '{remoteUrl}').");

            // 2. Assemble PAT-bearing URL: https://x-access-token:TOKEN@github.com/ORG/REPO.git
            string authenticatedUrl = BuildAuthenticatedUrl(remoteUrl, token);

            // 3. Push HEAD -> remote branch in one shot, disabling helpers that might prompt/override
            var pushCmd = $"-c credential.helper=\"\" push \"{authenticatedUrl}\" HEAD:{_defaultBranch}";

            await _retry429.ExecuteAsync(async () =>
                           {
                               // IMPORTANT: do not log this command; it contains the token in the URL.
                               await Run(pushCmd, directory, log: false, cancellationToken: cancellationToken)
                                   .NoSync();
                           })
                           .NoSync();

            _logger.LogInformation("Successfully pushed to {Dir}", directory);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not push in {Dir}", directory);
            throw;
        }
    }

    private async ValueTask<string> GetRemoteUrl(string directory, CancellationToken cancellationToken)
    {
        try
        {
            List<string> output = await Run("remote get-url origin", directory, log: false, cancellationToken: cancellationToken)
                .NoSync();
            if (output.Count > 0 && !string.IsNullOrWhiteSpace(output[0]))
                return output[0]
                    .Trim();

            _logger.LogWarning("Could not determine remote URL for 'origin' in directory {Dir}. Push will likely fail.", directory);
            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get remote URL for {Dir}", directory);
            return string.Empty;
        }
    }

    public async ValueTask AddIfNotExists(string directory, string relativeOrAbsolutePath, CancellationToken cancellationToken = default)
    {
        string full = Path.IsPathRooted(relativeOrAbsolutePath) ? relativeOrAbsolutePath : Path.GetFullPath(Path.Combine(directory, relativeOrAbsolutePath));

        // Avoid allocating Path.GetFullPath(directory) twice
        string rootFull = Path.GetFullPath(directory);
        if (!full.StartsWith(rootFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("File is outside the repository root.");

        string rel = Path.GetRelativePath(directory, full)
                         .Replace('\\', '/'); // normalize for git pathspec

        // Non-erroring check: returns 0 lines if not tracked
        List<string> listed = await Run($"ls-files --cached -- \"{rel}\"", directory, log: false, cancellationToken: cancellationToken)
            .NoSync();
        if (listed.Count > 0)
            return;

        if (!File.Exists(full))
            throw new FileNotFoundException("File not found in working tree", full);

        const string forceFlag = ""; // kept for future toggling without branching allocations
        await Run($"add{forceFlag} -- \"{rel}\"", directory, cancellationToken: cancellationToken)
            .NoSync();
    }

    public async ValueTask Fetch(string directory, string? token = null, CancellationToken cancellationToken = default)
    {
        try
        {
            Dictionary<string, string> env = CreateAuthEnv(token);

            await Run("fetch origin --filter=blob:none --prune", directory, env: env, cancellationToken: cancellationToken)
                .NoSync();
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

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string repoRoot in EnumerateRepoRoots(directory))
            set.Add(repoRoot);

        return set.Count == 0 ? [] : [..set];
    }

    /// <summary>
    /// Recursively finds working-tree roots by looking for a valid <c>.git</c>
    /// control directory *or* control file. False-positives are eliminated by a cheap HEAD / gitdir check.
    /// </summary>
    private static IEnumerable<string> EnumerateRepoRoots(string root)
    {
        var opts = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.ReparsePoint, // no loops
            IgnoreInaccessible = true,
            MatchCasing = MatchCasing.CaseInsensitive
        };

        var enumerable = new FileSystemEnumerable<string>(root, static (ref e) => e.ToFullPath(), opts)
        {
            ShouldIncludePredicate = static (ref e) => e.FileName.Equals(".git", StringComparison.OrdinalIgnoreCase),

            // Never descend into a .git dir itself – eliminates double hits
            ShouldRecursePredicate = static (ref e) => !e.FileName.Equals(".git", StringComparison.OrdinalIgnoreCase)
        };

        foreach (string gitPath in enumerable)
        {
            if (!LooksLikeValidGitControlPath(gitPath))
                continue;

            yield return Path.GetDirectoryName(gitPath)!; // repo root
        }
    }

    private static bool LooksLikeValidGitControlPath(string gitPath)
    {
        // .git directory -> must contain HEAD file
        if (Directory.Exists(gitPath))
            return File.Exists(Path.Join(gitPath, "HEAD"));

        // .git file -> must start with "gitdir:" line (worktrees/submodules)
        if (!File.Exists(gitPath))
            return false;

        try
        {
            // Read only the first line; cheaper than File.ReadLines(...).FirstOrDefault()
            using var fs = new FileStream(gitPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, bufferSize: 256, FileOptions.SequentialScan);
            using var sr = new StreamReader(fs, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 256, leaveOpen: false);

            string? line = sr.ReadLine();
            return line is not null && line.StartsWith("gitdir:", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static string BuildAuthenticatedUrl(string remoteHttps, string token)
    {
        var builder = new UriBuilder(remoteHttps)
        {
            UserName = "x-access-token",
            Password = token
        };

        return builder.Uri.AbsoluteUri;
    }

    public async ValueTask<List<string>> GetAllDirtyRepositories(string directory, CancellationToken cancellationToken = default)
    {
        List<string> repos = GetAllGitRepositoriesRecursively(directory);
        var dirty = new ConcurrentBag<string>();

        await Parallel.ForEachAsync(repos, CreateParallelOptions(cancellationToken), async (repo, ct) =>
                      {
                          if (await IsRepositoryDirty(repo, ct)
                                  .NoSync())
                              dirty.Add(repo);
                      })
                      .NoSync();

        return dirty.ToList();
    }

    public async ValueTask CommitAndPush(string directory, string message, string token, string? name = null, string? email = null,
        CancellationToken cancellationToken = default)
    {
        if (!await IsRepositoryDirty(directory, cancellationToken)
                .NoSync())
        {
            _logger.LogInformation("No changes to commit in {Dir}", directory);
            return;
        }

        await Commit(directory, message, name, email, cancellationToken)
            .NoSync();
        await Push(directory, token, cancellationToken)
            .NoSync();
    }
}