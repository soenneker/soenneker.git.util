using Soenneker.Extensions.Configuration;
using Soenneker.Extensions.Task;
using Soenneker.Extensions.ValueTask;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using Soenneker.Git.Util.Abstract;
using Soenneker.Utils.Directory.Abstract;
using Soenneker.Utils.File.Abstract;
using Soenneker.Utils.Path.Abstract;
using Soenneker.Utils.Process.Abstract;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Soenneker.Extensions.String;
using Soenneker.Utils.Random;

namespace Soenneker.Git.Util;

/// <inheritdoc cref="IGitUtil"/>
public sealed partial class GitUtil : IGitUtil
{
    private readonly string _configToken;
    private readonly string _configName;
    private readonly string _configEmail;
    private readonly string _defaultBranch;
    private readonly bool _logGitCommands;

    private readonly ILogger<GitUtil> _logger;
    private readonly IDirectoryUtil _directoryUtil;
    private readonly IProcessUtil _processUtil;
    private readonly IPathUtil _pathUtil;
    private readonly IFileUtil _fileUtil;

    private readonly string _gitBinaryPath;
    private readonly AsyncRetryPolicy _retry429;

    // Cache Authorization header per token to avoid base64 work every call
    private readonly ConcurrentDictionary<string, string> _authHeaderCache = new(StringComparer.Ordinal);
    
    private readonly int _maxParallelism;

    private const string _gitTerminalPromptKey = "GIT_TERMINAL_PROMPT";
    private const string _gitTerminalPromptValue = "0";

    public GitUtil(IConfiguration config, ILogger<GitUtil> logger, IDirectoryUtil directoryUtil, IProcessUtil processUtil, IPathUtil pathUtil,
        IFileUtil fileUtil)
    {
        _logger = logger;
        _directoryUtil = directoryUtil;
        _processUtil = processUtil;
        _pathUtil = pathUtil;
        _fileUtil = fileUtil;

        // Capture config once – avoids mid-run reload surprises
        _configToken = config.GetValueStrict<string>("Git:Token");
        _configName = config.GetValueStrict<string>("Git:Name");
        _configEmail = config.GetValueStrict<string>("Git:Email");
        _defaultBranch = config.GetValue<string>("Git:DefaultBranch") ?? "main";
        _logGitCommands = config.GetValue<bool>("Git:Log");

        _gitBinaryPath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Path.Join(AppContext.BaseDirectory, "Resources", "win-x64", "git", "cmd", "git.exe")
            : Path.Join(AppContext.BaseDirectory, "Resources", "linux-x64", "git", "git.sh");

        _maxParallelism = Math.Min(Environment.ProcessorCount, 8);

        _retry429 = Policy
                    .Handle<InvalidOperationException>(static ex =>
                        ex.Message.Contains("429", StringComparison.OrdinalIgnoreCase) ||
                        ex.Message.Contains("Too Many Requests", StringComparison.OrdinalIgnoreCase) ||
                        ex.Message.Contains("rate limit", StringComparison.OrdinalIgnoreCase))
                    .WaitAndRetryAsync(
                        retryCount: 5,
                        sleepDurationProvider: static attempt =>
                            TimeSpan.FromSeconds(1 << attempt) + TimeSpan.FromMilliseconds(RandomUtil.Next(0, 500)),
                        onRetry: (ex, ts, attempt, _) =>
                            _logger.LogWarning("Git rate limit detected – retry #{Attempt} in {Delay:n1}s", attempt, ts.TotalSeconds));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ParallelOptions CreateParallelOptions(CancellationToken cancellationToken)
    {
        return new ParallelOptions
        {
            // Cap to 8 threads – enough for network-bound Git without overwhelming disks
            MaxDegreeOfParallelism = _maxParallelism,
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
    private Dictionary<string, string> GetAuthEnvCached(string? token)
    {
        token ??= _configToken ?? throw new InvalidOperationException("A token is required but none was provided.");

        return new Dictionary<string, string>(capacity: 5, StringComparer.Ordinal)
        {
            [_gitTerminalPromptKey] = _gitTerminalPromptValue,
            ["GIT_CONFIG_COUNT"] = "1",
            ["GIT_CONFIG_KEY_0"] = "http.https://github.com/.extraheader",
            ["GIT_CONFIG_VALUE_0"] = BuildAuthHeaderCached(token),
            ["GIT_ASKPASS"] = ""
        };
    }

    public async ValueTask<List<string>> Run(string arguments, string? workingDirectory = null, Dictionary<string, string>? env = null, bool log = true,
        CancellationToken cancellationToken = default)
    {
        if (_logGitCommands && log)
            _logger.LogInformation("[git] {GitBinary} {Arguments} (cwd: {Cwd})", _gitBinaryPath, arguments, workingDirectory ?? "<null>");

        Dictionary<string, string>? actualEnv = env;

        if (env is null)
        {
            actualEnv = new Dictionary<string, string>(1)
            {
                [_gitTerminalPromptKey] = _gitTerminalPromptValue
            };
        }
        else if (!env.ContainsKey(_gitTerminalPromptKey))
        {
            actualEnv = new Dictionary<string, string>(env.Count + 1, StringComparer.Ordinal);

            foreach (KeyValuePair<string, string> pair in env)
            {
                actualEnv[pair.Key] = pair.Value;
            }

            actualEnv[_gitTerminalPromptKey] = _gitTerminalPromptValue;
        }

        return await _processUtil.Start(_gitBinaryPath, workingDirectory, arguments, environmentalVars: actualEnv, log: log, cancellationToken: cancellationToken)
                                 .NoSync();
    }

    private async Task ForEachRepo(List<string> repos, bool parallel, CancellationToken ct, Func<string, CancellationToken, ValueTask> action)
    {
        if (parallel)
        {
            await Parallel.ForEachAsync(repos, CreateParallelOptions(ct), async (repo, token) =>
                          {
                              await action(repo, token)
                                  .NoSync();
                          })
                          .NoSync();

            return;
        }

        foreach (string repo in repos)
        {
            ct.ThrowIfCancellationRequested();
            await action(repo, ct)
                .NoSync();
        }
    }

    public async ValueTask SwitchToRemoteBranch(string directory, string? token = null, CancellationToken cancellationToken = default)
    {
        try
        {
            Dictionary<string, string> env = GetAuthEnvCached(token);

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
        if (await HasWorkingTreeChanges(directory, cancellationToken)
                .NoSync())
            return true;

        return await HasRemoteDiverged(directory, cancellationToken)
            .NoSync();
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
            Dictionary<string, string> env = GetAuthEnvCached(token);

            string args = shallow
                ? $"clone --filter=blob:none --depth=1 \"{uri}\" \"{directory}\""
                : $"clone \"{uri}\" \"{directory}\"";

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
                await _directoryUtil.Delete(dir, cancellationToken)
                                    .NoSync();
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
            Dictionary<string, string> env = GetAuthEnvCached(token);

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
            await Run("add -A", directory, cancellationToken: cancellationToken)
                .NoSync();

            bool hasStagedChanges = await HasStagedChanges(directory, cancellationToken)
                .NoSync();
            if (!hasStagedChanges)
            {
                _logger.LogInformation("No changes detected in {Dir}", directory);
                return;
            }

            var env = new Dictionary<string, string>(capacity: 5)
            {
                ["GIT_AUTHOR_NAME"] = name,
                ["GIT_AUTHOR_EMAIL"] = email,
                ["GIT_COMMITTER_NAME"] = name,
                ["GIT_COMMITTER_EMAIL"] = email,
                [_gitTerminalPromptKey] = _gitTerminalPromptValue
            };

            string msgFile = await _pathUtil.GetRandomTempFilePath(".tmp", cancellationToken)
                                            .NoSync();
            await _fileUtil.Write(msgFile, message, true, cancellationToken)
                           .NoSync();

            try
            {
                await Run($"commit -F \"{msgFile}\"", directory, env: env, cancellationToken: cancellationToken)
                    .NoSync();
            }
            finally
            {
                await _fileUtil.TryDeleteIfExists(msgFile, true, cancellationToken)
                               .NoSync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not commit in {Dir}", directory);
        }
    }

    public async ValueTask<bool> HasStagedChanges(string directory, CancellationToken cancellationToken = default)
    {
        try
        {
            await Run("diff --cached --quiet", directory, log: false, cancellationToken: cancellationToken)
                .NoSync();
            return false;
        }
        catch
        {
            // Assumes your process util throws on non-zero exit code.
            return true;
        }
    }

    public async ValueTask<bool> HasWorkingTreeChanges(string directory, CancellationToken cancellationToken = default)
    {
        try
        {
            List<string> output = await Run("status --porcelain", directory, log: false, cancellationToken: cancellationToken)
                .NoSync();
            return output.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    public async ValueTask Push(string directory, string token, CancellationToken cancellationToken = default)
    {
        try
        {
            Dictionary<string, string> env = GetAuthEnvCached(token);

            const string pushCmd = "-c credential.helper= -c core.askPass= push origin HEAD";

            await _retry429.ExecuteAsync(async () =>
                           {
                               await Run(pushCmd, directory, env: env, log: false, cancellationToken: cancellationToken)
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

    public async ValueTask AddIfNotExists(string directory, string relativeOrAbsolutePath, CancellationToken cancellationToken = default)
    {
        string full = Path.IsPathRooted(relativeOrAbsolutePath) ? relativeOrAbsolutePath : Path.GetFullPath(Path.Combine(directory, relativeOrAbsolutePath));

        // Cheap escape check: if the relative path escapes root (".."), reject
        string rel = Path.GetRelativePath(directory, full);

        if (Path.IsPathRooted(rel) || (rel.Length >= 2 && rel[0] == '.' && rel[1] == '.' &&
                                       (rel.Length == 2 || rel[2] == Path.DirectorySeparatorChar || rel[2] == Path.AltDirectorySeparatorChar)))
        {
            throw new InvalidOperationException("File is outside the repository root.");
        }

        rel = rel.Replace('\\', '/'); // normalize for git pathspec

        // Non-erroring check: returns 0 lines if not tracked
        List<string> listed = await Run($"ls-files --cached -- \"{rel}\"", directory, log: false, cancellationToken: cancellationToken)
            .NoSync();
        if (listed.Count > 0)
            return;

        if (!await _fileUtil.Exists(full, cancellationToken)
                            .NoSync())
            throw new FileNotFoundException("File not found in working tree", full);

        await Run($"add -- \"{rel}\"", directory, cancellationToken: cancellationToken)
            .NoSync();
    }

    public async ValueTask Fetch(string directory, string? token = null, CancellationToken cancellationToken = default)
    {
        try
        {
            Dictionary<string, string> env = GetAuthEnvCached(token);

            await Run("fetch origin --filter=blob:none --prune", directory, env: env, cancellationToken: cancellationToken)
                .NoSync();
            _logger.LogInformation("Fetched {Dir}", directory);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not fetch in {Dir}", directory);
        }
    }

    public async ValueTask<List<string>> GetAllGitRepositoriesRecursively(string directory, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Scanning for git repositories under {Root}", directory);

        var result = new List<string>();
        HashSet<string>? seen = null;

        await foreach (string repoRoot in EnumerateRepoRoots(directory, cancellationToken))
        {
            if (result.Count == 0)
            {
                result.Add(repoRoot);
                continue;
            }

            seen ??= new HashSet<string>(result, StringComparer.OrdinalIgnoreCase);

            if (seen.Add(repoRoot))
                result.Add(repoRoot);
        }

        return result;
    }

    /// <summary>
    /// Recursively finds working-tree roots by looking for a valid <c>.git</c>
    /// control directory *or* control file. False-positives are eliminated by a cheap HEAD / gitdir check.
    /// </summary>
    private async IAsyncEnumerable<string> EnumerateRepoRoots(string root, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (root.IsNullOrWhiteSpace())
            yield break;

        if (!await _directoryUtil.Exists(root, cancellationToken)
                                 .NoSync())
            yield break;

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
            cancellationToken.ThrowIfCancellationRequested();

            if (!await LooksLikeValidGitControlPath(gitPath)
                    .NoSync())
                continue;

            string? repoRoot = Path.GetDirectoryName(gitPath);

            if (repoRoot.HasContent())
                yield return repoRoot;
        }
    }

    private async ValueTask<bool> LooksLikeValidGitControlPath(string gitPath)
    {
        // .git directory -> must contain HEAD file
        if (await _directoryUtil.Exists(gitPath)
                                .NoSync())
            return await _fileUtil.Exists(Path.Join(gitPath, "HEAD"))
                                  .NoSync();

        // .git file -> must start with "gitdir:" line (worktrees/submodules)
        if (!await _fileUtil.Exists(gitPath)
                            .NoSync())
            return false;

        try
        {
            await using var fs = new FileStream(gitPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, bufferSize: 64, FileOptions.SequentialScan);

            Span<byte> buf = stackalloc byte[7]; // "gitdir:"
            int read = fs.Read(buf);
            if (read < 7)
                return false;

            // ASCII compare, case-insensitive for letters
            return (buf[0] | 0x20) == (byte)'g' && (buf[1] | 0x20) == (byte)'i' && (buf[2] | 0x20) == (byte)'t' && (buf[3] | 0x20) == (byte)'d' &&
                   (buf[4] | 0x20) == (byte)'i' && (buf[5] | 0x20) == (byte)'r' && buf[6] == (byte)':';
        }
        catch
        {
            return false;
        }
    }

    public async ValueTask<List<string>> GetAllDirtyRepositories(string directory, CancellationToken cancellationToken = default)
    {
        List<string> repos = await GetAllGitRepositoriesRecursively(directory, cancellationToken)
            .NoSync();
        if (repos.Count == 0)
            return [];

        var dirty = new ConcurrentQueue<string>();

        await Parallel.ForEachAsync(repos, CreateParallelOptions(cancellationToken), async (repo, ct) =>
                      {
                          if (await IsRepositoryDirty(repo, ct)
                                  .NoSync())
                              dirty.Enqueue(repo);
                      })
                      .NoSync();

        return [.. dirty];
    }

    public async ValueTask CommitAndPush(string directory, string message, string token, string? name = null, string? email = null,
        CancellationToken cancellationToken = default)
    {
        if (!await HasWorkingTreeChanges(directory, cancellationToken)
                .NoSync())
        {
            _logger.LogInformation("No local changes to commit in {Dir}", directory);
            return;
        }

        await Commit(directory, message, name, email, cancellationToken)
            .NoSync();
        await Push(directory, token, cancellationToken)
            .NoSync();
    }
}