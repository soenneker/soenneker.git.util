using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Git.Util.Abstract;

/// <summary>
/// High-level, asynchronous helpers for working with Git repositories –
/// cloning, fetching, pulling, committing, pushing and bulk operations.
/// </summary>
public interface IGitUtil
{
    /// <summary>
    /// Performs a <c>git pull --ff-only</c> against every repository discovered
    /// beneath <paramref name="root"/>.
    /// </summary>
    /// <param name="root">Root directory to scan for repositories.</param>
    /// <param name="token">
    /// Personal-access token used for authentication; if <see langword="null"/>,
    /// the default token from configuration is used.
    /// </param>
    /// <param name="parallel">Run pulls in parallel when <see langword="true"/>.</param>
    /// <param name="cancellationToken">Token that propagates cancellation.</param>
    ValueTask PullAllGitRepositories(string root, string? token = null, bool parallel = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs <c>git fetch</c> for every repository found under <paramref name="root"/>.
    /// </summary>
    ValueTask FetchAllGitRepositories(string root, string? token = null, bool parallel = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// For each repository beneath <paramref name="root"/>, checks out the
    /// configured default branch and hard-resets it to the corresponding
    /// remote branch (<c>origin/&lt;defaultBranch&gt;</c>).
    /// </summary>
    ValueTask SwitchAllGitRepositoriesToRemoteBranch(string root, string? token = null, bool parallel = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stages <c>-A</c> and commits every dirty repository under
    /// <paramref name="root"/> with <paramref name="commitMessage"/>.
    /// </summary>
    ValueTask CommitAllRepositories(string root, string commitMessage, bool parallel = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pushes the configured default branch for each repository found under
    /// <paramref name="root"/>.
    /// </summary>
    ValueTask PushAllRepositories(string root, string token, bool parallel = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Force-switches the specified repository to the default remote branch.
    /// </summary>
    ValueTask SwitchToRemoteBranch(string directory, string? token = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether the repository has uncommitted or untracked changes.
    /// </summary>
    ValueTask<bool> IsRepositoryDirty(string directory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether <paramref name="directory"/> is the root
    /// of a Git work-tree.
    /// </summary>
    ValueTask<bool> IsRepository(string directory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clones <paramref name="uri"/> into <paramref name="directory"/>.
    /// </summary>
    ValueTask Clone(string uri, string directory, string? token = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clones <paramref name="uri"/> into a newly created temporary directory
    /// and returns that directory’s path.
    /// </summary>
    ValueTask<string> CloneToTempDirectory(string uri, string? token = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an arbitrary Git <paramref name="command"/> in
    /// <paramref name="directory"/>.
    /// </summary>
    ValueTask RunCommand(string command, string directory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs a <c>git pull --ff-only</c> on the specified repository.
    /// </summary>
    ValueTask Pull(string directory, string? token = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds <c>-A</c>, then commits using the supplied metadata.  
    /// If the repository has no changes, the call is a no-op.
    /// </summary>
    ValueTask Commit(string directory, string message, string? name = null, string? email = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pushes the default branch of <paramref name="directory"/> to its
    /// <c>origin</c> remote.
    /// </summary>
    ValueTask Push(string directory, string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds <paramref name="relativeFilePath"/> to the index only if it is not
    /// already tracked.
    /// </summary>
    ValueTask AddIfNotExists(string directory, string relativeFilePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs <c>git fetch</c> on the specified repository.
    /// </summary>
    ValueTask Fetch(string directory, string? token = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Recursively scans <paramref name="directory"/> and returns every path
    /// that represents a Git repository root.
    /// </summary>
    ValueTask<List<string>> GetAllGitRepositoriesRecursively(string directory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the subset of repositories under <paramref name="directory"/>
    /// that currently have uncommitted changes.
    /// </summary>
    ValueTask<List<string>> GetAllDirtyRepositories(string directory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience helper that commits (if dirty) and pushes the specified
    /// repository in one call.
    /// </summary>
    ValueTask CommitAndPush(string directory, string message, string token, string? name = null, string? email = null,
        CancellationToken cancellationToken = default);
}