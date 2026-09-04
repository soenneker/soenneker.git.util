using Soenneker.Extensions.Configuration;
using Soenneker.Extensions.Task;
using Soenneker.Extensions.ValueTask;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Kevlar;
using Soenneker.Git.Util.Abstract;
using Soenneker.Utils.Directory.Abstract;
using Soenneker.Utils.File.Abstract;
using Soenneker.Utils.Path.Abstract;
using Soenneker.Utils.Process.Abstract;
using Soenneker.Utils.Paths.Resources.Abstract;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Soenneker.Utils.Random;
using Soenneker.Utils.Runtime;

namespace Soenneker.Git.Util;

/// <inheritdoc cref="IGitUtil" />
public sealed partial class GitUtil : IGitUtil
{
    private readonly string _configToken;
    private readonly string _configName;
    private readonly string _configEmail;
    private readonly string _defaultBranch;
    private readonly bool _logGitCommands;
    private readonly string _pullArguments;
    private readonly string _mergeBaseArguments;
    private readonly string _checkoutArguments;
    private readonly Dictionary<string, string> _defaultCommitEnvironment;

    private readonly ILogger<GitUtil> _logger;
    private readonly IDirectoryUtil _directoryUtil;
    private readonly IProcessUtil _processUtil;
    private readonly IPathUtil _pathUtil;
    private readonly IFileUtil _fileUtil;
    private readonly IResourcesPathUtil _resourcesPathUtil;

    private readonly string _gitBinaryRelativePath;
    private readonly SemaphoreSlim _gitBinaryPathLock = new(1, 1);
    private string? _gitBinaryPath;
    private readonly Shield _retry429;

    // Cache Authorization header per token to avoid base64 work every call
    private readonly ConcurrentDictionary<string, string> _authHeaderCache = new(StringComparer.Ordinal);
    
    private readonly int _maxParallelism;

    private const string _gitTerminalPromptKey = "GIT_TERMINAL_PROMPT";
    private const string _gitTerminalPromptValue = "0";

    private static readonly Dictionary<string, string> _defaultGitEnvironment = new(1, StringComparer.Ordinal)
    {
        [_gitTerminalPromptKey] = _gitTerminalPromptValue
    };

    private readonly ConcurrentDictionary<string, Dictionary<string, string>> _authEnvironmentCache = new(StringComparer.Ordinal);

    public GitUtil(IConfiguration config, ILogger<GitUtil> logger, IDirectoryUtil directoryUtil, IProcessUtil processUtil, IPathUtil pathUtil,
        IFileUtil fileUtil)
        : this(config, logger, directoryUtil, processUtil, pathUtil, fileUtil, new Soenneker.Utils.Paths.Resources.ResourcesPathUtil(directoryUtil))
    {
    }

    public GitUtil(IConfiguration config, ILogger<GitUtil> logger, IDirectoryUtil directoryUtil, IProcessUtil processUtil, IPathUtil pathUtil,
        IFileUtil fileUtil, IResourcesPathUtil resourcesPathUtil)
    {
        _logger = logger;
        _directoryUtil = directoryUtil;
        _processUtil = processUtil;
        _pathUtil = pathUtil;
        _fileUtil = fileUtil;
        _resourcesPathUtil = resourcesPathUtil;

        // Capture config once – avoids mid-run reload surprises
        _configToken = config.GetValueStrict<string>("Git:Token");
        _configName = config.GetValueStrict<string>("Git:Name");
        _configEmail = config.GetValueStrict<string>("Git:Email");
        _defaultBranch = config.GetValue<string>("Git:DefaultBranch") ?? "main";
        _logGitCommands = config.GetValue<bool>("Git:Log");

        _pullArguments = $"pull origin {_defaultBranch}";
        _mergeBaseArguments = $"merge-base --is-ancestor HEAD origin/{_defaultBranch}";
        _checkoutArguments = $"checkout -B {_defaultBranch} origin/{_defaultBranch}";
        _defaultCommitEnvironment = CreateCommitEnvironment(_configName, _configEmail);

        _gitBinaryRelativePath = RuntimeUtil.IsWindows()
            ? Path.Join("win-x64", "git", "cmd", "git.exe")
            : Path.Join("linux-x64", "git", "git.sh");

        _maxParallelism = Math.Min(Environment.ProcessorCount, 8);

        _retry429 = Shield
                    .When<InvalidOperationException>(static ex =>
                        ex.Message.Contains("429", StringComparison.OrdinalIgnoreCase) ||
                        ex.Message.Contains("Too Many Requests", StringComparison.OrdinalIgnoreCase) ||
                        ex.Message.Contains("rate limit", StringComparison.OrdinalIgnoreCase))
                    .Retry(options =>
                    {
                        options.MaxRetries = 5;
                        options.Backoff = Backoff.Custom(static attempt =>
                            TimeSpan.FromSeconds(1 << attempt) + TimeSpan.FromMilliseconds(RandomUtil.Next(0, 500)));
                        options.OnRetry = retry =>
                        {
                            _logger.LogWarning("Git rate limit detected – retry #{Attempt} in {Delay:n1}s", retry.AttemptNumber + 1,
                                retry.Delay.TotalSeconds);
                            return default;
                        };
                    });
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

        return _authEnvironmentCache.GetOrAdd(token, static (t, self) => new Dictionary<string, string>(capacity: 5, StringComparer.Ordinal)
        {
            [_gitTerminalPromptKey] = _gitTerminalPromptValue,
            ["GIT_CONFIG_COUNT"] = "1",
            ["GIT_CONFIG_KEY_0"] = "http.https://github.com/.extraheader",
            ["GIT_CONFIG_VALUE_0"] = self.BuildAuthHeaderCached(t),
            ["GIT_ASKPASS"] = ""
        }, this);
    }

    private static Dictionary<string, string> CreateCommitEnvironment(string name, string email)
    {
        return new Dictionary<string, string>(capacity: 5, StringComparer.Ordinal)
        {
            ["GIT_AUTHOR_NAME"] = name,
            ["GIT_AUTHOR_EMAIL"] = email,
            ["GIT_COMMITTER_NAME"] = name,
            ["GIT_COMMITTER_EMAIL"] = email,
            [_gitTerminalPromptKey] = _gitTerminalPromptValue
        };
    }

    public async ValueTask<List<string>> Run(string arguments, string? workingDirectory = null, Dictionary<string, string>? env = null, bool log = true,
        CancellationToken cancellationToken = default)
    {
        string gitBinaryPath = await GetGitBinaryPath(cancellationToken)
            .NoSync();

        if (_logGitCommands && log)
            _logger.LogInformation("[git] {GitBinary} {Arguments} (cwd: {Cwd})", gitBinaryPath, arguments, workingDirectory ?? "<null>");

        Dictionary<string, string>? actualEnv = env;

        if (env is null)
            actualEnv = _defaultGitEnvironment;
        else if (!env.ContainsKey(_gitTerminalPromptKey))
        {
            actualEnv = new Dictionary<string, string>(env.Count + 1, StringComparer.Ordinal);

            foreach (KeyValuePair<string, string> pair in env)
            {
                actualEnv[pair.Key] = pair.Value;
            }

            actualEnv[_gitTerminalPromptKey] = _gitTerminalPromptValue;
        }

        return await _processUtil.Start(gitBinaryPath, workingDirectory, arguments, environmentalVars: actualEnv, log: log, cancellationToken: cancellationToken)
                                 .NoSync();
    }

    private async ValueTask<string> GetGitBinaryPath(CancellationToken cancellationToken)
    {
        string? cached = Volatile.Read(ref _gitBinaryPath);
        if (cached is not null)
            return cached;

        await _gitBinaryPathLock.WaitAsync(cancellationToken)
                                .NoSync();

        try
        {
            cached = _gitBinaryPath;
            if (cached is not null)
                return cached;

            cached = await _resourcesPathUtil.GetResourceFilePath(_gitBinaryRelativePath, cancellationToken)
                                                .NoSync();
            Volatile.Write(ref _gitBinaryPath, cached);
            return cached;
        }
        finally
        {
            _gitBinaryPathLock.Release();
        }
    }

    private async ValueTask ForEachRepo(List<string> repos, bool parallel, CancellationToken ct, Func<string, CancellationToken, ValueTask> action)
    {
        if (parallel)
        {
            await Parallel.ForEachAsync(repos, CreateParallelOptions(ct), action)
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

            await Run("fetch origin --filter=blob:none --prune", directory, env: env, cancellationToken: cancellationToken)
                .NoSync();

            if (await HasWorkingTreeChanges(directory, cancellationToken).NoSync())
                throw new InvalidOperationException($"Refusing to switch {directory} because it contains working-tree changes.");

            await Run(_mergeBaseArguments, directory, log: false, cancellationToken: cancellationToken)
                .NoSync();

            await Run(_checkoutArguments, directory, cancellationToken: cancellationToken)
                .NoSync();

            _logger.LogInformation("Switched {Dir} to remote branch '{Branch}'", directory, _defaultBranch);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not switch to remote branch for {Dir}", directory);
            throw;
        }
    }

    public async ValueTask<bool> IsRepositoryDirty(string directory, CancellationToken cancellationToken = default)
    {
        try
        {
            List<string> lines = await Run("-c safe.directory=* status --porcelain=v2 --branch", directory, log: false,
                    cancellationToken: cancellationToken)
                .NoSync();

            foreach (string line in lines)
            {
                ReadOnlySpan<char> value = line.AsSpan();
                if (value.IsEmpty)
                    continue;

                if (value[0] != '#')
                    return true;

                const string branchAheadBehindPrefix = "# branch.ab ";
                if (!value.StartsWith(branchAheadBehindPrefix, StringComparison.Ordinal))
                    continue;

                ReadOnlySpan<char> aheadBehind = value[branchAheadBehindPrefix.Length..];
                if (!aheadBehind.SequenceEqual("+0 -0"))
                    return true;
            }

            return false;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not determine repository status for {Dir}", directory);
            throw;
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

            await Run(_pullArguments, directory, env: env, cancellationToken: cancellationToken)
                .NoSync();
            _logger.LogDebug("Pulled latest changes for {Dir}", directory);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not pull in {Dir}", directory);
            throw;
        }
    }

    public async ValueTask Commit(string directory, string message, string? name = null, string? email = null, CancellationToken cancellationToken = default)
    {
        await TryCommit(directory, message, name, email, cancellationToken)
            .NoSync();
    }

    private async ValueTask<bool> TryCommit(string directory, string message, string? name, string? email, CancellationToken cancellationToken)
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
                return false;
            }

            Dictionary<string, string> env = string.Equals(name, _configName, StringComparison.Ordinal) &&
                                             string.Equals(email, _configEmail, StringComparison.Ordinal)
                ? _defaultCommitEnvironment
                : CreateCommitEnvironment(name, email);

            string msgFile = await _pathUtil.GetRandomTempFilePath(".tmp", cancellationToken)
                                            .NoSync();
            await _fileUtil.Write(msgFile, message, true, cancellationToken)
                           .NoSync();

            try
            {
                await Run($"commit -F \"{msgFile}\"", directory, env: env, cancellationToken: cancellationToken)
                    .NoSync();

                return true;
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
            throw;
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
            List<string> output = await Run("-c safe.directory=* status --porcelain", directory, log: false, cancellationToken: cancellationToken)
                .NoSync();
            return output.Count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not determine working tree status for {Dir}", directory);
            throw;
        }
    }

    public async ValueTask Push(string directory, string token, CancellationToken cancellationToken = default)
    {
        try
        {
            Dictionary<string, string> env = GetAuthEnvCached(token);

            const string pushCmd = "-c credential.helper= -c core.askPass= push origin HEAD";

            await _retry429.ExecuteAsync(async token =>
                           {
                               await Run(pushCmd, directory, env: env, log: false, cancellationToken: token)
                                   .NoSync();
                           }, cancellationToken)
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
            throw;
        }
    }

    public ValueTask<List<string>> GetAllGitRepositoriesRecursively(string directory, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Scanning for git repositories under {Root}", directory);

        var result = new List<string>();

        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            return ValueTask.FromResult(result);

        var opts = new EnumerationOptions
        {
            RecurseSubdirectories = false,
            AttributesToSkip = FileAttributes.ReparsePoint,
            IgnoreInaccessible = true,
            MatchCasing = MatchCasing.CaseInsensitive
        };

        var pending = new Queue<string>();
        pending.Enqueue(Path.GetFullPath(directory));
        int partitionTarget = Math.Max(4, _maxParallelism * 4);

        // Split near the root first. Each remaining subtree can then be scanned independently without
        // synchronizing for every directory or filesystem entry.
        while (pending.Count > 0 && pending.Count < partitionTarget)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string currentDirectory = pending.Dequeue();

            var entries = new FileSystemEnumerable<GitFileSystemEntry>(currentDirectory,
                static (ref FileSystemEntry e) => new GitFileSystemEntry(e.ToFullPath(), e.IsDirectory,
                    e.FileName.Equals(".git", StringComparison.OrdinalIgnoreCase)), opts)
            {
                // Do not allocate paths for ordinary files. We only need child directories and .git control files.
                ShouldIncludePredicate = static (ref FileSystemEntry e) =>
                    e.IsDirectory || e.FileName.Equals(".git", StringComparison.OrdinalIgnoreCase)
            };

            bool foundRepository = false;

            foreach (GitFileSystemEntry entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (entry.IsGitControlPath)
                {
                    // A .git directory is repository metadata, never another search root.
                    if (!foundRepository && LooksLikeValidGitControlPath(entry.Path, entry.IsDirectory))
                    {
                        result.Add(currentDirectory);
                        foundRepository = true;
                    }

                    continue;
                }

                pending.Enqueue(entry.Path);
            }
        }

        if (pending.Count == 0)
            return ValueTask.FromResult(result);

        var recursiveOpts = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            IgnoreInaccessible = true,
            MatchCasing = MatchCasing.CaseInsensitive
        };
        var discovered = new ConcurrentQueue<string>();

        Parallel.ForEach(pending, CreateParallelOptions(cancellationToken), seed =>
        {
            var entries = new FileSystemEnumerable<GitFileSystemEntry>(seed,
                static (ref FileSystemEntry e) => new GitFileSystemEntry(e.ToFullPath(), e.IsDirectory,
                    e.FileName.Equals(".git", StringComparison.OrdinalIgnoreCase)), recursiveOpts)
            {
                ShouldIncludePredicate = static (ref FileSystemEntry e) => e.FileName.Equals(".git", StringComparison.OrdinalIgnoreCase),
                ShouldRecursePredicate = static (ref FileSystemEntry e) => !e.FileName.Equals(".git", StringComparison.OrdinalIgnoreCase)
            };

            foreach (GitFileSystemEntry entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (LooksLikeValidGitControlPath(entry.Path, entry.IsDirectory))
                    discovered.Enqueue(Path.GetDirectoryName(entry.Path)!);
            }
        });

        if (!discovered.IsEmpty)
        {
            var seen = new HashSet<string>(result, StringComparer.OrdinalIgnoreCase);

            while (discovered.TryDequeue(out string? repoRoot))
            {
                if (seen.Add(repoRoot))
                    result.Add(repoRoot);
            }
        }

        return ValueTask.FromResult(result);
    }

    private static bool LooksLikeValidGitControlPath(string gitPath, bool isDirectory)
    {
        if (isDirectory)
            return File.Exists(Path.Join(gitPath, "HEAD"));

        try
        {
            Span<byte> buf = stackalloc byte[7];
            using var handle = File.OpenHandle(gitPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            int read = RandomAccess.Read(handle, buf, 0);
            if (read < 7)
                return false;

            return (buf[0] | 0x20) == (byte)'g' && (buf[1] | 0x20) == (byte)'i' && (buf[2] | 0x20) == (byte)'t' && (buf[3] | 0x20) == (byte)'d' &&
                   (buf[4] | 0x20) == (byte)'i' && (buf[5] | 0x20) == (byte)'r' && buf[6] == (byte)':';
        }
        catch
        {
            return false;
        }
    }

    private readonly record struct GitFileSystemEntry(string Path, bool IsDirectory, bool IsGitControlPath);

    public async ValueTask<List<string>> GetAllDirtyRepositories(string directory, CancellationToken cancellationToken = default)
    {
        List<string> repos = await GetAllGitRepositoriesRecursively(directory, cancellationToken)
            .NoSync();
        if (repos.Count == 0)
            return [];

        bool[] dirty = ArrayPool<bool>.Shared.Rent(repos.Count);
        Array.Clear(dirty, 0, repos.Count);

        try
        {
            await Parallel.ForAsync(0, repos.Count, CreateParallelOptions(cancellationToken), async (index, ct) =>
                          {
                              dirty[index] = await IsRepositoryDirty(repos[index], ct)
                                  .NoSync();
                          })
                          .NoSync();

            var result = new List<string>();

            for (var i = 0; i < repos.Count; i++)
            {
                if (dirty[i])
                    result.Add(repos[i]);
            }

            return result;
        }
        finally
        {
            ArrayPool<bool>.Shared.Return(dirty, clearArray: true);
        }
    }

    public async ValueTask CommitAndPush(string directory, string message, string token, string? name = null, string? email = null,
        CancellationToken cancellationToken = default)
    {
        if (!await TryCommit(directory, message, name, email, cancellationToken)
                .NoSync())
        {
            _logger.LogInformation("No local changes to commit in {Dir}", directory);
            return;
        }

        await Push(directory, token, cancellationToken)
            .NoSync();
    }
}
