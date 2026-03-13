using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Git.Util.Abstract;

/// <summary>
/// High-level, asynchronous helpers for working with Git repositories,
/// including discovery, cloning, fetching, pulling, committing, pushing,
/// and bulk repository operations.
/// </summary>
public interface IGitUtil
{
    /// <summary>
    /// Performs a pull against every Git repository discovered beneath <paramref name="root"/>.
    /// </summary>
    /// <param name="root">Root directory to scan for repositories.</param>
    /// <param name="token">
    /// Personal access token used for authentication. If <see langword="null"/>,
    /// the default token from configuration is used.
    /// </param>
    /// <param name="parallel">
    /// Whether repository operations should be performed in parallel.
    /// </param>
    /// <param name="cancellationToken">Token that propagates cancellation.</param>
    ValueTask PullAllGitRepositories(string root, string? token = null, bool parallel = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs a fetch against every Git repository discovered beneath <paramref name="root"/>.
    /// </summary>
    /// <param name="root">Root directory to scan for repositories.</param>
    /// <param name="token">
    /// Personal access token used for authentication. If <see langword="null"/>,
    /// the default token from configuration is used.
    /// </param>
    /// <param name="parallel">
    /// Whether repository operations should be performed in parallel.
    /// </param>
    /// <param name="cancellationToken">Token that propagates cancellation.</param>
    ValueTask FetchAllGitRepositories(string root, string? token = null, bool parallel = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// For every Git repository discovered beneath <paramref name="root"/>,
    /// fetches, checks out the configured default branch, and hard-resets it
    /// to the corresponding remote branch.
    /// </summary>
    /// <param name="root">Root directory to scan for repositories.</param>
    /// <param name="token">
    /// Personal access token used for authentication. If <see langword="null"/>,
    /// the default token from configuration is used.
    /// </param>
    /// <param name="parallel">
    /// Whether repository operations should be performed in parallel.
    /// </param>
    /// <param name="cancellationToken">Token that propagates cancellation.</param>
    ValueTask SwitchAllGitRepositoriesToRemoteBranch(string root, string? token = null, bool parallel = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits all repositories discovered beneath <paramref name="root"/>
    /// using the supplied commit message.
    /// </summary>
    /// <param name="root">Root directory to scan for repositories.</param>
    /// <param name="commitMessage">Commit message to use.</param>
    /// <param name="parallel">
    /// Whether repository operations should be performed in parallel.
    /// </param>
    /// <param name="cancellationToken">Token that propagates cancellation.</param>
    ValueTask CommitAllRepositories(string root, string commitMessage, bool parallel = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pushes every Git repository discovered beneath <paramref name="root"/>.
    /// </summary>
    /// <param name="root">Root directory to scan for repositories.</param>
    /// <param name="token">Personal access token used for authentication.</param>
    /// <param name="parallel">
    /// Whether repository operations should be performed in parallel.
    /// </param>
    /// <param name="cancellationToken">Token that propagates cancellation.</param>
    ValueTask PushAllRepositories(string root, string token, bool parallel = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// For every Git repository discovered beneath <paramref name="root"/>,
    /// performs a pull and then a push.
    /// </summary>
    /// <param name="root">Root directory to scan for repositories.</param>
    /// <param name="token">Personal access token used for authentication.</param>
    /// <param name="parallel">
    /// Whether repository operations should be performed in parallel.
    /// </param>
    /// <param name="cancellationToken">Token that propagates cancellation.</param>
    ValueTask PullAndPushAllRepositories(string root, string token, bool parallel = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches, checks out the configured default branch, and hard-resets it
    /// to the corresponding remote branch for the specified repository.
    /// </summary>
    /// <param name="directory">Repository directory.</param>
    /// <param name="token">
    /// Personal access token used for authentication. If <see langword="null"/>,
    /// the default token from configuration is used.
    /// </param>
    /// <param name="cancellationToken">Token that propagates cancellation.</param>
    ValueTask SwitchToRemoteBranch(string directory, string? token = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether the specified directory contains a Git working tree.
    /// </summary>
    /// <param name="directory">Directory to inspect.</param>
    /// <param name="cancellationToken">Token that propagates cancellation.</param>
    ValueTask<bool> IsRepository(string directory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether the repository has local working tree changes or has diverged from its upstream.
    /// </summary>
    /// <param name="directory">Repository directory.</param>
    /// <param name="cancellationToken">Token that propagates cancellation.</param>
    ValueTask<bool> IsRepositoryDirty(string directory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether the repository has staged changes.
    /// </summary>
    /// <param name="directory">Repository directory.</param>
    /// <param name="cancellationToken">Token that propagates cancellation.</param>
    ValueTask<bool> HasStagedChanges(string directory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether the repository has working tree changes.
    /// </summary>
    /// <param name="directory">Repository directory.</param>
    /// <param name="cancellationToken">Token that propagates cancellation.</param>
    ValueTask<bool> HasWorkingTreeChanges(string directory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clones <paramref name="uri"/> into <paramref name="directory"/>.
    /// </summary>
    /// <param name="uri">Repository URI.</param>
    /// <param name="directory">Destination directory.</param>
    /// <param name="token">
    /// Personal access token used for authentication. If <see langword="null"/>,
    /// the default token from configuration is used.
    /// </param>
    /// <param name="shallow">Whether to perform a shallow clone.</param>
    /// <param name="cancellationToken">Token that propagates cancellation.</param>
    ValueTask Clone(string uri, string directory, string? token = null, bool shallow = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clones <paramref name="uri"/> into a newly created temporary directory
    /// and returns that directory path.
    /// </summary>
    /// <param name="uri">Repository URI.</param>
    /// <param name="token">
    /// Personal access token used for authentication. If <see langword="null"/>,
    /// the default token from configuration is used.
    /// </param>
    /// <param name="cancellationToken">Token that propagates cancellation.</param>
    ValueTask<string> CloneToTempDirectory(string uri, string? token = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the Git binary with the supplied arguments.
    /// </summary>
    /// <param name="arguments">Command-line arguments to pass to Git.</param>
    /// <param name="workingDirectory">Optional working directory.</param>
    /// <param name="env">Optional environment variables.</param>
    /// <param name="log">Whether command execution should be logged.</param>
    /// <param name="cancellationToken">Token that propagates cancellation.</param>
    ValueTask<List<string>> Run(string arguments, string? workingDirectory = null, Dictionary<string, string>? env = null, bool log = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs a pull for the specified repository.
    /// </summary>
    /// <param name="directory">Repository directory.</param>
    /// <param name="token">
    /// Personal access token used for authentication. If <see langword="null"/>,
    /// the default token from configuration is used.
    /// </param>
    /// <param name="cancellationToken">Token that propagates cancellation.</param>
    ValueTask Pull(string directory, string? token = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stages all changes and commits them using the supplied commit message and identity.
    /// If there are no staged changes after staging, the operation is a no-op.
    /// </summary>
    /// <param name="directory">Repository directory.</param>
    /// <param name="message">Commit message.</param>
    /// <param name="name">Optional Git author/committer name.</param>
    /// <param name="email">Optional Git author/committer email.</param>
    /// <param name="cancellationToken">Token that propagates cancellation.</param>
    ValueTask Commit(string directory, string message, string? name = null, string? email = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pushes the current HEAD of the specified repository to its origin remote.
    /// </summary>
    /// <param name="directory">Repository directory.</param>
    /// <param name="token">Personal access token used for authentication.</param>
    /// <param name="cancellationToken">Token that propagates cancellation.</param>
    ValueTask Push(string directory, string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds the specified file to the index only if it is not already tracked.
    /// </summary>
    /// <param name="directory">Repository directory.</param>
    /// <param name="relativeOrAbsolutePath">Path to add, relative to the repository root or absolute.</param>
    /// <param name="cancellationToken">Token that propagates cancellation.</param>
    ValueTask AddIfNotExists(string directory, string relativeOrAbsolutePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs a fetch for the specified repository.
    /// </summary>
    /// <param name="directory">Repository directory.</param>
    /// <param name="token">
    /// Personal access token used for authentication. If <see langword="null"/>,
    /// the default token from configuration is used.
    /// </param>
    /// <param name="cancellationToken">Token that propagates cancellation.</param>
    ValueTask Fetch(string directory, string? token = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Recursively scans <paramref name="directory"/> and returns every Git repository root found.
    /// </summary>
    /// <param name="directory">Root directory to scan.</param>
    /// <param name="cancellationToken">Token that propagates cancellation.</param>
    ValueTask<List<string>> GetAllGitRepositoriesRecursively(string directory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the subset of Git repositories under <paramref name="directory"/>
    /// that are currently dirty.
    /// </summary>
    /// <param name="directory">Root directory to scan.</param>
    /// <param name="cancellationToken">Token that propagates cancellation.</param>
    ValueTask<List<string>> GetAllDirtyRepositories(string directory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits local changes, if present, and then pushes the repository.
    /// </summary>
    /// <param name="directory">Repository directory.</param>
    /// <param name="message">Commit message.</param>
    /// <param name="token">Personal access token used for authentication.</param>
    /// <param name="name">Optional Git author/committer name.</param>
    /// <param name="email">Optional Git author/committer email.</param>
    /// <param name="cancellationToken">Token that propagates cancellation.</param>
    ValueTask CommitAndPush(string directory, string message, string token, string? name = null, string? email = null,
        CancellationToken cancellationToken = default);
}