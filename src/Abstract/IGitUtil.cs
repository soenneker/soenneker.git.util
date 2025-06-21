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
    void Clone(string uri, string directory);

    /// <summary>
    /// Clones a Git repository into a temporary directory.
    /// </summary>
    /// <returns>The path of the temporary directory the repository was cloned into.</returns>
    string CloneToTempDirectory(string uri);

    /// <summary>
    /// Pulls the latest changes for all Git repositories recursively found in the specified directory.
    /// </summary>
    void PullAllGitRepositories(string directory);

    /// <summary>
    /// Fetches all remote changes for all Git repositories recursively found in the specified directory.
    /// </summary>
    void FetchAllGitRepositories(string directory);

    /// <summary>
    /// Switches all repositories in the specified directory to the tracked remote branch (main).
    /// </summary>
    void SwitchAllGitRepositoriesToRemoteBranch(string directory);

    /// <summary>
    /// Commits all repositories with the given message.
    /// </summary>
    void CommitAllRepositories(string directory, string commitMessage);

    /// <summary>
    /// Pushes all repositories in the directory using the given credentials.
    /// </summary>
    ValueTask PushAllRepositories(string directory, string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Switches the specified repository to the main remote branch (origin/main).
    /// </summary>
    void SwitchToRemoteBranch(string directory);

    /// <summary>
    /// Determines whether the given directory contains a Git repository with uncommitted changes.
    /// </summary>
    bool IsRepositoryDirty(string directory);

    /// <summary>
    /// Determines whether the specified directory is a valid Git repository.
    /// </summary>
    bool IsRepository(string directory);

    /// <summary>
    /// Executes a raw Git command in the given directory.
    /// </summary>
    ValueTask RunCommand(string command, string directory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pulls changes from the remote repository into the specified local repository.
    /// </summary>
    void Pull(string directory, string? name = null, string? email = null);

    /// <summary>
    /// Commits changes in the given repository using the specified message and author info.
    /// </summary>
    void Commit(string directory, string message, string? name = null, string? email = null);

    /// <summary>
    /// Pushes commits from the given repository to its remote using provided credentials.
    /// </summary>
    ValueTask Push(string directory, string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stages a file if it is not already present in the index.
    /// </summary>
    void AddIfNotExists(string directory, string relativeFilePath);

    /// <summary>
    /// Fetches changes from the remote repository without merging them.
    /// </summary>
    void Fetch(string directory);

    /// <summary>
    /// Recursively retrieves all directories containing valid Git repositories within the given path.
    /// </summary>
    List<string> GetAllGitRepositoriesRecursively(string directory);

    /// <summary>
    /// Retrieves all dirty repositories (with uncommitted changes) under the specified path.
    /// </summary>
    List<string> GetAllDirtyRepositories(string directory);

    /// <summary>
    /// Commits and pushes a repository using the given credentials and message.
    /// </summary>
    ValueTask CommitAndPush(string directory, string name, string email, string token, string message, CancellationToken cancellationToken = default);
}
