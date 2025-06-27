using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using Soenneker.Extensions.Configuration;
using Soenneker.Extensions.ValueTask;
using Soenneker.Git.Util.Abstract;
using Soenneker.Utils.Directory;
using Soenneker.Utils.Directory.Abstract;
using Soenneker.Utils.Process.Abstract;
using Soenneker.Utils.Runtime;

namespace Soenneker.Git.Util;

///<inheritdoc cref="IGitUtil"/>
public sealed class GitUtil : IGitUtil
{
    private readonly IConfiguration _config;
    private readonly ILogger<GitUtil> _logger;
    private readonly IDirectoryUtil _directoryUtil;
    private readonly IProcessUtil _processUtil;

    private readonly Lazy<AsyncRetryPolicy> _tooManyRequestsRetryPolicy;

    public GitUtil(IConfiguration config, ILogger<GitUtil> logger, IDirectoryUtil directoryUtil, IProcessUtil processUtil)
    {
        _config = config;
        _logger = logger;
        _directoryUtil = directoryUtil;
        _processUtil = processUtil;

        _tooManyRequestsRetryPolicy = new Lazy<AsyncRetryPolicy>(() => Policy.Handle<HttpRequestException>(ex =>
                                                                             {
                                                                                 if (ex.StatusCode == HttpStatusCode.TooManyRequests)
                                                                                 {
                                                                                     _logger.LogWarning("429 detected: slowing our requests...");
                                                                                     return true;
                                                                                 }

                                                                                 return false;
                                                                             })
                                                                             .WaitAndRetryAsync(retryCount: 5,
                                                                                 sleepDurationProvider: retryAttempt =>
                                                                                     TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                                                                                 onRetry: (exception, timeSpan, retryCount, context) =>
                                                                                 {
                                                                                     _logger.LogWarning(
                                                                                         $"Retry {retryCount} after {timeSpan.TotalSeconds} seconds due to: {exception.Message}");
                                                                                 }));
    }

    // TODO: Probably should break these 'bulk' operations into a separate class

    public string GetGitBinaryPath()
    {
        if (RuntimeUtil.IsWindows())
        {
            return Path.Combine(AppContext.BaseDirectory, "Resources", "win-x64", "git", "cmd", "git.exe");
        }

        if (RuntimeUtil.IsLinux())
        {
            return Path.Combine(AppContext.BaseDirectory, "Resources", "linux-x64", "git", "bin", "git");
        }

        throw new PlatformNotSupportedException("Unsupported platform for Git binary path retrieval.");
    }

    public async ValueTask PullAllGitRepositories(string directory, CancellationToken cancellationToken = default)
    {
        List<string> allRepos = await GetAllGitRepositoriesRecursively(directory, cancellationToken).NoSync();

        foreach (string repo in allRepos)
        {
            await Pull(repo, null, null, cancellationToken).NoSync();
        }
    }

    public async ValueTask FetchAllGitRepositories(string directory, CancellationToken cancellationToken = default)
    {
        List<string> allRepos = await GetAllGitRepositoriesRecursively(directory, cancellationToken).NoSync();

        foreach (string repo in allRepos)
        {
            await Fetch(repo, cancellationToken).NoSync();
        }
    }

    public async ValueTask SwitchAllGitRepositoriesToRemoteBranch(string directory, CancellationToken cancellationToken = default)
    {
        List<string> allRepos = await GetAllGitRepositoriesRecursively(directory, cancellationToken).NoSync();

        foreach (string repo in allRepos)
        {
            await SwitchToRemoteBranch(repo, cancellationToken).NoSync();
        }
    }

    public async ValueTask CommitAllRepositories(string directory, string commitMessage, CancellationToken cancellationToken = default)
    {
        List<string> allRepos = await GetAllGitRepositoriesRecursively(directory, cancellationToken).NoSync();

        foreach (string repo in allRepos)
        {
            await Commit(repo, commitMessage, null, null, cancellationToken).NoSync();
        }
    }

    public async ValueTask PushAllRepositories(string directory, string token, CancellationToken cancellationToken = default)
    {
        List<string> allRepos = await GetAllGitRepositoriesRecursively(directory, cancellationToken).NoSync();

        foreach (string repo in allRepos)
        {
            await Push(repo, token, cancellationToken).NoSync();
        }
    }

    public async ValueTask SwitchToRemoteBranch(string directory, CancellationToken cancellationToken = default)
    {
        try
        {
            await _processUtil.Start(GetGitBinaryPath(), directory, "fetch origin", true, cancellationToken: cancellationToken).NoSync();
            await _processUtil.Start(GetGitBinaryPath(), directory, "checkout main", true, cancellationToken: cancellationToken).NoSync();
            await _processUtil.Start(GetGitBinaryPath(), directory, "reset --hard origin/main", true, cancellationToken: cancellationToken).NoSync();
            _logger.LogInformation("Switched to remote branch 'main' in directory {directory}.", directory);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Could not switch to remote branch for directory {dir}", directory);
        }
    }

    public async ValueTask<bool> IsRepositoryDirty(string directory, CancellationToken cancellationToken = default)
    {
        try
        {
            List<string> outputLines = await _processUtil.Start(GetGitBinaryPath(), directory, "status --porcelain", true, cancellationToken: cancellationToken).NoSync();
            return outputLines.Count > 0;
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
            List<string> outputLines = await _processUtil.Start(GetGitBinaryPath(), directory, "rev-parse --is-inside-work-tree", true, cancellationToken: cancellationToken).NoSync();
            return outputLines.Count > 0 && outputLines[0].Trim() == "true";
        }
        catch
        {
            return false;
        }
    }

    public async ValueTask Clone(string uri, string directory, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Cloning uri ({uri}) into directory ({dir}) ...", uri, directory);
        var token = _config.GetValueStrict<string>("Git:Token");
        string uriWithToken = uri.Replace("https://", $"https://x-access-token:{token}@");
        try
        {
            await _processUtil.Start(GetGitBinaryPath(), null, $"clone {uriWithToken} \"{directory}\"", true, cancellationToken: cancellationToken).NoSync();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Could not clone uri ({uri}) into directory ({dir})", uri, directory);
        }
        _logger.LogDebug("Finished cloning uri ({uri}) into directory ({dir})", uri, directory);
    }

    public async ValueTask<string> CloneToTempDirectory(string uri, CancellationToken cancellationToken = default)
    {
        string dir = await _directoryUtil.CreateTempDirectory(cancellationToken).NoSync();
        await Clone(uri, dir, cancellationToken).NoSync();
        return dir;
    }

    public async ValueTask RunCommand(string command, string directory, CancellationToken cancellationToken = default)
    {
        await _processUtil.Start(GetGitBinaryPath(), directory, command, true, cancellationToken: cancellationToken);
    }

    public async ValueTask Pull(string directory, string? name = null, string? email = null, CancellationToken cancellationToken = default)
    {
        name ??= _config.GetValueStrict<string>("Git:Name");
        email ??= _config.GetValueStrict<string>("Git:Email");
        var token = _config.GetValueStrict<string>("Git:Token");
        try
        {
            await _processUtil.Start(GetGitBinaryPath(), directory, $"config user.name \"{name}\"", true, cancellationToken: cancellationToken).NoSync();
            await _processUtil.Start(GetGitBinaryPath(), directory, $"config user.email \"{email}\"", true, cancellationToken: cancellationToken).NoSync();
            List<string> remoteUrlLines = await _processUtil.Start(GetGitBinaryPath(), directory, "remote get-url origin", true, cancellationToken: cancellationToken).NoSync();
            string? remoteUrl = remoteUrlLines.Count > 0 ? remoteUrlLines[0].Trim() : null;
            if (!string.IsNullOrEmpty(remoteUrl) && remoteUrl.StartsWith("https://"))
            {
                string urlWithToken = remoteUrl.Replace("https://", $"https://x-access-token:{token}@");
                await _processUtil.Start(GetGitBinaryPath(), directory, $"remote set-url origin {urlWithToken}", true, cancellationToken: cancellationToken).NoSync();
            }
            try
            {
                await _processUtil.Start(GetGitBinaryPath(), directory, "pull origin main", true, cancellationToken: cancellationToken).NoSync();
                _logger.LogDebug("Completed pull");
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Conflicted for repo in directory ({directory}), cannot merge!", directory);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Could not pull in directory ({directory})", directory);
        }
    }

    public async ValueTask Commit(string directory, string message, string? name = null, string? email = null, CancellationToken cancellationToken = default)
    {
        name ??= _config.GetValueStrict<string>("Git:Name");
        email ??= _config.GetValueStrict<string>("Git:Email");

        try
        {
            if (!await IsRepositoryDirty(directory, cancellationToken).NoSync())
            {
                _logger.LogInformation("No changes detected to commit for directory ({directory}), skipping", directory);
                return;
            }

            await _processUtil.Start(GetGitBinaryPath(), directory, $"config user.name \"{name}\"", true, cancellationToken: cancellationToken).NoSync();
            await _processUtil.Start(GetGitBinaryPath(), directory, $"config user.email \"{email}\"", true, cancellationToken: cancellationToken).NoSync();
            await _processUtil.Start(GetGitBinaryPath(), directory, "add -A", true, cancellationToken: cancellationToken).NoSync();
            await _processUtil.Start(GetGitBinaryPath(), directory, $"commit -m \"{message}\"", true, cancellationToken: cancellationToken).NoSync();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Could not commit for directory ({dir})", directory);
        }
    }

    public async ValueTask Push(string directory, string token, CancellationToken cancellationToken = default)
    {
        try
        {
            // Set remote url with token
            List<string> remoteUrlLines =
                await _processUtil.Start(GetGitBinaryPath(), directory, "remote get-url origin", true, cancellationToken: cancellationToken).NoSync();
            string? remoteUrl = remoteUrlLines.Count > 0 ? remoteUrlLines[0].Trim() : null;
            if (!string.IsNullOrEmpty(remoteUrl) && remoteUrl.StartsWith("https://"))
            {
                string urlWithToken = remoteUrl.Replace("https://", $"https://x-access-token:{token}@");
                await _processUtil.Start(GetGitBinaryPath(), directory, $"remote set-url origin {urlWithToken}", true, cancellationToken: cancellationToken).NoSync();
            }

            // Push
            await _tooManyRequestsRetryPolicy.Value.ExecuteAsync(() =>
                Task.Run(async () => { await _processUtil.Start(GetGitBinaryPath(), directory, "push origin main", true, cancellationToken: cancellationToken).NoSync(); },
                    cancellationToken));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Could not push for directory ({dir})", directory);
        }
    }

    public async ValueTask AddIfNotExists(string directory, string relativeFilePath, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Adding file ({file}) to index if it doesn't exist ...", relativeFilePath);
        try
        {
            List<string> outputLines = await _processUtil.Start(GetGitBinaryPath(), directory, "diff --name-only --cached", true, cancellationToken: cancellationToken).NoSync();
            List<string> stagedFiles = outputLines.Select(f => f.Trim()).ToList();
            if (stagedFiles.Contains(relativeFilePath))
                return;
            await _processUtil.Start(GetGitBinaryPath(), directory, $"add \"{relativeFilePath}\"", true, cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Could not add {relativeFilePath} to index", relativeFilePath);
            throw;
        }
    }

    public async ValueTask Fetch(string directory, CancellationToken cancellationToken = default)
    {
        var token = _config.GetValueStrict<string>("Git:Token");
        try
        {
            List<string> remoteUrlLines = await _processUtil.Start(GetGitBinaryPath(), directory, "remote get-url origin", true, cancellationToken: cancellationToken).NoSync();
            string? remoteUrl = remoteUrlLines.Count > 0 ? remoteUrlLines[0].Trim() : null;
            if (!string.IsNullOrEmpty(remoteUrl) && remoteUrl.StartsWith("https://"))
            {
                string urlWithToken = remoteUrl.Replace("https://", $"https://x-access-token:{token}@");
                await _processUtil.Start(GetGitBinaryPath(), directory, $"remote set-url origin {urlWithToken}", true, cancellationToken: cancellationToken).NoSync();
            }

            await _processUtil.Start(GetGitBinaryPath(), directory, "fetch origin", true, cancellationToken: cancellationToken).NoSync();
            _logger.LogInformation("Completed fetch");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Could not fetch for directory ({dir})", directory);
        }
    }

    public async ValueTask<List<string>> GetAllGitRepositoriesRecursively(string directory, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting all git repositories recursively in directory ({directory})...", directory);
        var finalDirectories = new List<string>();
        List<string> orderedDirectories = DirectoryUtil.GetDirectoriesOrderedByLevels(directory);
        orderedDirectories.RemoveAll(c => c.Contains(Path.DirectorySeparatorChar + ".git"));
        var index = 0;

        while (index < orderedDirectories.Count)
        {
            string item = orderedDirectories[index];
            if (await IsRepository(item, cancellationToken).NoSync())
            {
                finalDirectories.Add(item);
                orderedDirectories.RemoveAll(dir => dir.StartsWith(item + Path.DirectorySeparatorChar));
            }
            index++;
        }
        return finalDirectories;
    }

    public async ValueTask<List<string>> GetAllDirtyRepositories(string directory, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting all 'dirty' repositories in directory ({directory})...", directory);
        List<string> allRepos = await GetAllGitRepositoriesRecursively(directory, cancellationToken).NoSync();
        var result = new List<string>();

        foreach (string repo in allRepos)
        {
            if (await IsRepositoryDirty(repo, cancellationToken).NoSync())
            {
                result.Add(repo);
            }
        }
        return result;
    }

    public async ValueTask CommitAndPush(string directory, string name, string email, string token, string message,
        CancellationToken cancellationToken = default)
    {
        if (!await IsRepositoryDirty(directory, cancellationToken).NoSync())
        {
            _logger.LogInformation("No changes to commit.");
            return;
        }

        await Commit(directory, message, name, email, cancellationToken).NoSync();
        await Push(directory, token, cancellationToken).NoSync();
    }
}