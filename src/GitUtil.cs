using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LibGit2Sharp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Soenneker.Extensions.Configuration;
using Soenneker.Git.Util.Abstract;
using Soenneker.Utils.Directory.Abstract;

namespace Soenneker.Git.Util;

///<inheritdoc cref="IGitUtil"/>
public class GitUtil : IGitUtil
{
    private readonly IConfiguration _config;
    private readonly ILogger<GitUtil> _logger;
    private readonly IDirectoryUtil _directoryUtil;

    public GitUtil(IConfiguration config, ILogger<GitUtil> logger, IDirectoryUtil directoryUtil)
    {
        _config = config;
        _logger = logger;
        _directoryUtil = directoryUtil;
    }
    
    // TODO: Probably should break these 'bulk' operations into a separate class

    public void PullAllGitRepositories(string directory)
    {
        List<string> allRepos = GetAllGitRepositoriesRecursively(directory);

        foreach (string repo in allRepos)
        {
            Pull(repo);
        }
    }

    public void FetchAllGitRepositories(string directory)
    {
        List<string> allRepos = GetAllGitRepositoriesRecursively(directory);

        foreach (string repo in allRepos)
        {
            Fetch(repo);
        }
    }

    public void SwitchAllGitRepositoriesToRemoteBranch(string directory)
    {
        List<string> allRepos = GetAllGitRepositoriesRecursively(directory);

        foreach (string repo in allRepos)
        {
            SwitchToRemoteBranch(repo);
        }
    }

    public void CommitAllRepositories(string directory, string commitMessage)
    {
        List<string> allRepos = GetAllGitRepositoriesRecursively(directory);

        foreach (string repo in allRepos)
        {
            Commit(repo, commitMessage);
        }
    }

    public async ValueTask PushAllRepositories(string directory, string username, string token, bool delayOnSuccess = true)
    {
        List<string> allRepos = GetAllGitRepositoriesRecursively(directory);

        foreach (string repo in allRepos)
        {
            await Push(repo, username, token, delayOnSuccess);
        }
    }

    public void SwitchToRemoteBranch(string directory)
    {
        try
        {
            using (var repo = new Repository(directory))
            {
                Remote? remote = repo.Network.Remotes["origin"];

                _logger.LogInformation("Switching to remote branch from {url} in directory {directory}...", remote.Url, directory);

                const string trackedBranchName = "main";
                Branch mainBranch = repo.Branches[trackedBranchName];
                Commands.Checkout(repo, mainBranch, new CheckoutOptions { CheckoutModifiers = CheckoutModifiers.Force });
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Could not switch to remove branch for directory {dir}", directory);
        }
    }

    public bool IsRepositoryDirty(string directory)
    {
        using (var repo = new Repository(directory))
        {
            RepositoryStatus status = repo.RetrieveStatus();
            return status.IsDirty;
        }
    }

    public bool IsRepository(string directory)
    {
        return Repository.IsValid(directory);
    }

    public void Clone(string uri, string directory)
    {
        _logger.LogInformation("Cloning uri ({uri}) into directory ({dir}) ...", uri, directory);

        try
        {
            Repository.Clone(uri, directory);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Could not clone uri ({uri}) into directory ({dir})", uri, directory);
        }

        _logger.LogDebug("Finished cloning uri ({uri}) into directory ({dir})", uri, directory);
    }

    public string CloneToTempDirectory(string uri)
    {
        string dir = _directoryUtil.CreateTempDirectory();

        Clone(uri, dir);

        return dir;
    }

    public void Pull(string directory, string? name = null, string? email = null)
    {
        string? url = null;

        name ??= _config.GetValueStrict<string>("Git:Name");
        email ??= _config.GetValueStrict<string>("Git:Email");

        try
        {
            using (var repo = new Repository(directory))
            {
                Remote? remote = repo.Network.Remotes["origin"];

                url = remote.Url;

                _logger.LogInformation("Pulling from ({url}) in directory ({directory})...", url, directory);

                MergeResult? mergeResult = Commands.Pull(repo, new Signature(name, email, DateTimeOffset.UtcNow),
                    new PullOptions
                    {
                        FetchOptions = new FetchOptions(),
                        MergeOptions = new MergeOptions
                        {
                            FailOnConflict = true,
                            CommitOnSuccess = false
                        }
                    });

                if (mergeResult.Status == MergeStatus.Conflicts)
                    _logger.LogError("Conflicted for repo ({url}) in directory ({directory}), cannot merge!", url, directory);
                else
                    _logger.LogDebug("Completed pull");
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Could not pull ({url}) in directory ({directory})", url, directory);
        }
    }

    public void Commit(string directory, string message, string? name = null, string? email = null)
    {
        name ??= _config.GetValueStrict<string>("Git:Name");
        email ??= _config.GetValueStrict<string>("Git:Email");

        try
        {
            if (!IsRepositoryDirty(directory))
            {
                _logger.LogInformation("No changes detected to commit for directory ({directory}), skipping", directory);
                return;
            }

            using (var repo = new Repository(directory))
            {
                _logger.LogInformation("Committing changes in directory ({directory}) ...", directory);

                var signature = new Signature(name, email, DateTimeOffset.UtcNow);

                // Adds files that are not indexed yet
                Commands.Stage(repo, "*");
                repo.Commit(message, signature, signature);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Could not commit for directory ({dir})", directory);
        }
    }

    public async ValueTask Push(string directory, string username, string token, bool delayOnSuccess = true)
    {
        try
        {
            using (var repo = new Repository(directory))
            {
                Remote? remote = repo.Network.Remotes["origin"];

                _logger.LogInformation("Pushing changes to repo ({url}) in directory ({directory}) ...", remote.Url, directory);

                if (!HasChangesToPush(repo))
                {
                    return;
                }

                var options = new PushOptions
                {
                    CredentialsProvider = (url, usernameFromUrl, types) =>
                        new UsernamePasswordCredentials
                        {
                            Username = username,
                            Password = token
                        }
                };

                Branch localMainBranch = repo.Branches["refs/heads/main"];

                repo.Network.Push(localMainBranch, options);

                if (delayOnSuccess)
                    await Task.Delay(1000).ConfigureAwait(false);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Could not push for directory ({dir})", directory);
        }
    }

    public void AddIfNotExists(string directory, string relativeFilePath)
    {
        _logger.LogDebug("Adding file ({file}) to index if it doesn't exist ...", relativeFilePath);

        try
        {
            using (var repo = new Repository(directory))
            {
                if (IsFileInIndex(repo, relativeFilePath))
                    return;

                Commands.Stage(repo, relativeFilePath);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public void Fetch(string directory)
    {
        try
        {
            using (var repo = new Repository(directory))
            {
                Remote? remote = repo.Network.Remotes["origin"];

                _logger.LogInformation("Fetching from {url} in ({directory}) ...", remote.Url, directory);

                Commands.Fetch(repo, remote.Url, new string[] { },
                    new FetchOptions(), "");

                _logger.LogInformation("Completed fetch");
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Could not fetch for directory ({dir})", directory);
        }
    }

    public List<string> GetAllGitRepositoriesRecursively(string directory)
    {
        List<string> directories = _directoryUtil.GetAllDirectoriesRecursively(directory);

        var gitRepos = new List<string>();

        foreach (string dir in directories)
        {
            if (!IsRepository(dir))
                continue;

            if (!dir.EndsWith(".git"))
                gitRepos.Add(dir);
        }

        return gitRepos;
    }

    public List<string> GetAllDirtyRepositories(string directory)
    {
        List<string> allRepos = GetAllGitRepositoriesRecursively(directory);

        var result = new List<string>();

        foreach (string repo in allRepos)
        {
            if (IsRepositoryDirty(repo))
            {
                result.Add(repo);
            }
        }

        return result;
    }


    private static bool IsFileInIndex(Repository repo, string relativeFilePath)
    {
        foreach (IndexEntry? entry in repo.Index)
        {
            if (entry.Path == relativeFilePath)
            {
                return true;
            }
        }

        return false;
    }

    private bool HasChangesToPush(IRepository repo)
    {
        Branch localMainBranch = repo.Branches["refs/heads/main"];

        Commit? localHeadCommit = localMainBranch.Commits.FirstOrDefault();

        if (localHeadCommit == null)
        {
            _logger.LogInformation("No changes detected for path ({path}), skipping push", repo.Info.Path);
            return false;
        }

        Branch remoteMainBranch = repo.Branches["refs/remotes/origin/main"];

        Commit? remoteHeadCommit = remoteMainBranch.Commits.FirstOrDefault();

        if (remoteHeadCommit == null)
        {
            _logger.LogInformation("No changes detected for path ({path}), skipping push", repo.Info.Path);
            return false;
        }

        if (localHeadCommit.Id == remoteHeadCommit.Id)
        {
            _logger.LogInformation("No changes detected for path ({path}), skipping push", repo.Info.Path);
            return false;
        }

        return true;
    }
}