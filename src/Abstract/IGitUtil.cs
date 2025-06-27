using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Git.Util.Abstract;

/// <summary>
/// A utility interface for managing Git repositories using LibGit2Sharp and custom operations.
/// </summary>
public interface IGitUtil
{
    /// <summary>
    /// Clones a Git repository to the specified directory.
    /// </summary>
    ValueTask Clone(string uri, string directory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clones a Git repository into a temporary directory.
    /// </summary>
    /// <returns>The path of the temporary directory the repository was cloned into.</returns>
    ValueTask<string> CloneToTempDirectory(string uri, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pulls the latest changes for all Git repositories recursively found in the specified directory.
    /// </summary>
    ValueTask PullAllGitRepositories(string directory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches all remote changes for all Git repositories recursively found in the specified directory.
    /// </summary>
    ValueTask FetchAllGitRepositories(string directory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Switches all repositories in the specified directory to the tracked remote branch (main).
    /// </summary>
    ValueTask SwitchAllGitRepositoriesToRemoteBranch(string directory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits all repositories with the given message.
    /// </summary>
    ValueTask CommitAllRepositories(string directory, string commitMessage, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pushes all repositories in the directory using the given credentials.
    /// </summary>
    ValueTask PushAllRepositories(string directory, string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Switches the specified repository to the main remote branch (origin/main).
    /// </summary>
    ValueTask SwitchToRemoteBranch(string directory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether the given directory contains a Git repository with uncommitted changes.
    /// </summary>
    ValueTask<bool> IsRepositoryDirty(string directory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether the specified directory is a valid Git repository.
    /// </summary>
    ValueTask<bool> IsRepository(string directory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a raw Git command in the given directory.
    /// </summary>
    ValueTask RunCommand(string command, string directory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pulls changes from the remote repository into the specified local repository.
    /// </summary>
    ValueTask Pull(string directory, string? name = null, string? email = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits changes in the given repository using the specified message and author info.
    /// </summary>
    ValueTask Commit(string directory, string message, string? name = null, string? email = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pushes commits from the given repository to its remote using provided credentials.
    /// </summary>
    ValueTask Push(string directory, string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stages a file if it is not already present in the index.
    /// </summary>
    ValueTask AddIfNotExists(string directory, string relativeFilePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches changes from the remote repository without merging them.
    /// </summary>
    ValueTask Fetch(string directory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Recursively retrieves all directories containing valid Git repositories within the given path.
    /// </summary>
    ValueTask<List<string>> GetAllGitRepositoriesRecursively(string directory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all dirty repositories (with uncommitted changes) under the specified path.
    /// </summary>
    ValueTask<List<string>> GetAllDirtyRepositories(string directory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits and pushes a repository using the given credentials and message.
    /// </summary>
    ValueTask CommitAndPush(string directory, string name, string email, string token, string message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the full path to the git binary for the current runtime.
    /// </summary>
    string GetGitBinaryPath();
}
