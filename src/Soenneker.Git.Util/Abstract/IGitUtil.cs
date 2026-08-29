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
    /// Pulls all Git Repositories.
    /// </summary>
    /// <param name="root">Root directory or repository to process.</param>
    /// <param name="token">Arbitrary utility token to append.</param>
    /// <param name="parallel">Whether repository operations should be performed in parallel.</param>
    /// <param name="cancellationToken">Token that propagates cancellation.</param>
    /// <returns>A task that completes when the pull all git repositories operation is complete.</returns>
    ValueTask PullAllGitRepositories(string root, string? token = null, bool parallel = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches all Git Repositories.
    /// </summary>
    /// <param name="root">Root directory or repository to process.</param>
    /// <param name="token">Arbitrary utility token to append.</param>
    /// <param name="parallel">Whether repository operations should be performed in parallel.</param>
    /// <param name="cancellationToken">Token that propagates cancellation.</param>
    /// <returns>A task that completes when the fetch all git repositories operation is complete.</returns>
    ValueTask FetchAllGitRepositories(string root, string? token = null, bool parallel = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the Git multi-pack-index file for every repository discovered beneath <paramref name="root"/>.
    /// </summary>
    /// <param name="root">Root directory to scan for repositories.</param>
    /// <param name="parallel">Whether repository operations should be performed in parallel.</param>
    /// <param name="cancellationToken">Token that propagates cancellation.</param>
    /// <returns>A task that completes after the targeted files have been deleted.</returns>
    ValueTask DeleteMultiPackIndexesForAllRepositories(string root, bool parallel = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rebuilds the Git multi-pack-index for every repository discovered beneath <paramref name="root"/>.
    /// </summary>
    /// <param name="root">Root directory to scan for repositories.</param>
    /// <param name="parallel">Whether repository operations should be performed in parallel.</param>
    /// <param name="cancellationToken">Token that propagates cancellation.</param>
    /// <returns>A task that completes when the repack indexes for all repositories operation is complete.</returns>
    ValueTask RepackIndexesForAllRepositories(string root, bool parallel = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs Git garbage collection for every repository discovered beneath <paramref name="root"/>.
    /// </summary>
    /// <param name="root">Root directory to scan for repositories.</param>
    /// <param name="parallel">Whether repository operations should be performed in parallel.</param>
    /// <param name="cancellationToken">Token that propagates cancellation.</param>
    /// <returns>A task that completes when the garbage collect all repositories operation is complete.</returns>
    ValueTask GarbageCollectAllRepositories(string root, bool parallel = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Garbage-collects all Repositories Or Reclone.
    /// </summary>
    /// <param name="root">Root directory or repository to process.</param>
    /// <param name="token">Arbitrary utility token to append.</param>
    /// <param name="parallel">Whether repository operations should be performed in parallel.</param>
    /// <param name="cancellationToken">Token that propagates cancellation.</param>
    /// <returns>A task that completes when the garbage collect all repositories or reclone operation is complete.</returns>
    ValueTask GarbageCollectAllRepositoriesOrReclone(string root, string? token = null, bool parallel = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs a full Git integrity check for every repository discovered beneath <paramref name="root"/>.
    /// </summary>
    /// <param name="root">Root directory to scan for repositories.</param>
    /// <param name="parallel">Whether repository operations should be performed in parallel.</param>
    /// <param name="cancellationToken">Token that propagates cancellation.</param>
    /// <returns>A task that completes when the integrity check all repositories operation is complete.</returns>
    ValueTask IntegrityCheckAllRepositories(string root, bool parallel = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Switches all Git Repositories To Remote Branch.
    /// </summary>
    /// <param name="root">Root directory or repository to process.</param>
    /// <param name="token">Arbitrary utility token to append.</param>
    /// <param name="parallel">Whether repository operations should be performed in parallel.</param>
    /// <param name="cancellationToken">Token that propagates cancellation.</param>
    /// <returns>A task that completes when the switch all git repositories to remote branch operation is complete.</returns>
    ValueTask SwitchAllGitRepositoriesToRemoteBranch(string root, string? token = null, bool parallel = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits all repositories discovered beneath <paramref name="root"/>
    /// using the supplied commit message.
    /// </summary>
    /// <param name="root">Root directory to scan for repositories.</param>
    /// <param name="commitMessage">Commit message to use.</param>
    /// <param name="parallel">Whether repository operations should be performed in parallel.</param>
    /// <param name="cancellationToken">Token that propagates cancellation.</param>
    /// <returns>A task that completes when the commit all repositories operation is complete.</returns>
    ValueTask CommitAllRepositories(string root, string commitMessage, bool parallel = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pushes every Git repository discovered beneath <paramref name="root"/>.
    /// </summary>
    /// <param name="root">Root directory to scan for repositories.</param>
    /// <param name="token">Personal access token used for authentication.</param>
    /// <param name="parallel">Whether repository operations should be performed in parallel.</param>
    /// <param name="cancellationToken">Token that propagates cancellation.</param>
    /// <returns>A task that completes when the push all repositories operation is complete.</returns>
    ValueTask PushAllRepositories(string root, string token, bool parallel = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// For every Git repository discovered beneath <paramref name="root"/>,
    /// performs a pull and then a push.
    /// </summary>
    /// <param name="root">Root directory to scan for repositories.</param>
    /// <param name="token">Personal access token used for authentication.</param>
    /// <param name="parallel">Whether repository operations should be performed in parallel.</param>
    /// <param name="cancellationToken">Token that propagates cancellation.</param>
    /// <returns>A task that completes when the pull and push all repositories operation is complete.</returns>
    ValueTask PullAndPushAllRepositories(string root, string token, bool parallel = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Switches to Remote Branch.
    /// </summary>
    /// <param name="directory">Directory to read from or write to.</param>
    /// <param name="token">Arbitrary utility token to append.</param>
    /// <param name="cancellationToken">Token that propagates cancellation.</param>
    /// <returns>A task that completes when the switch to remote branch operation is complete.</returns>
    ValueTask SwitchToRemoteBranch(string directory, string? token = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the Git multi-pack-index file for the specified repository.
    /// </summary>
    /// <param name="directory">Repository directory.</param>
    /// <param name="cancellationToken">Token that propagates cancellation.</param>
    /// <returns>A task that completes after the targeted files have been deleted.</returns>
    ValueTask DeleteMultiPackIndex(string directory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rebuilds Git pack indexes for the specified repository.
    /// </summary>
    /// <param name="directory">Repository directory.</param>
    /// <param name="cancellationToken">Token that propagates cancellation.</param>
    /// <returns>A task that completes when the repack indexes operation is complete.</returns>
    ValueTask RepackIndexes(string directory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs Git garbage collection for the specified repository.
    /// </summary>
    /// <param name="directory">Repository directory.</param>
    /// <param name="cancellationToken">Token that propagates cancellation.</param>
    /// <returns>A task that completes when the garbage collect operation is complete.</returns>
    ValueTask GarbageCollect(string directory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Garbage-collects or Reclone.
    /// </summary>
    /// <param name="directory">Directory to read from or write to.</param>
    /// <param name="token">Arbitrary utility token to append.</param>
    /// <param name="cancellationToken">Token that propagates cancellation.</param>
    /// <returns>A task that completes when the garbage collect or reclone operation is complete.</returns>
    ValueTask GarbageCollectOrReclone(string directory, string? token = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs a full Git integrity check for the specified repository.
    /// </summary>
    /// <param name="directory">Repository directory.</param>
    /// <param name="cancellationToken">Token that propagates cancellation.</param>
    /// <returns>true if runs a full Git integrity check for the specified repository; otherwise, false.</returns>
    ValueTask<bool> IntegrityCheck(string directory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether the specified directory contains a Git working tree.
    /// </summary>
    /// <param name="directory">Directory to inspect.</param>
    /// <param name="cancellationToken">Token that propagates cancellation.</param>
    /// <returns>true if the specified directory contains a Git working tree; otherwise, false.</returns>
    ValueTask<bool> IsRepository(string directory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether the repository has local working tree changes or has diverged from its upstream.
    /// </summary>
    /// <param name="directory">Repository directory.</param>
    /// <param name="cancellationToken">Token that propagates cancellation.</param>
    /// <returns>true if the repository has local working tree changes or has diverged from its upstream; otherwise, false.</returns>
    ValueTask<bool> IsRepositoryDirty(string directory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether the repository has staged changes.
    /// </summary>
    /// <param name="directory">Repository directory.</param>
    /// <param name="cancellationToken">Token that propagates cancellation.</param>
    /// <returns>true if the repository has staged changes; otherwise, false.</returns>
    ValueTask<bool> HasStagedChanges(string directory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether the repository has working tree changes.
    /// </summary>
    /// <param name="directory">Repository directory.</param>
    /// <param name="cancellationToken">Token that propagates cancellation.</param>
    /// <returns>true if the repository has working tree changes; otherwise, false.</returns>
    ValueTask<bool> HasWorkingTreeChanges(string directory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clones git.
    /// </summary>
    /// <param name="uri">Receives the normalized absolute URI when parsing succeeds.</param>
    /// <param name="directory">Directory to read from or write to.</param>
    /// <param name="token">Arbitrary utility token to append.</param>
    /// <param name="shallow">Whether to perform a shallow clone.</param>
    /// <param name="cancellationToken">Token that propagates cancellation.</param>
    /// <returns>A task that completes when the clone operation is complete.</returns>
    ValueTask Clone(string uri, string directory, string? token = null, bool shallow = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clones to Temp Directory.
    /// </summary>
    /// <param name="uri">Receives the normalized absolute URI when parsing succeeds.</param>
    /// <param name="token">Arbitrary utility token to append.</param>
    /// <param name="cancellationToken">Token that propagates cancellation.</param>
    /// <returns>A task whose result is the text returned by clone To Temp Directory.</returns>
    ValueTask<string> CloneToTempDirectory(string uri, string? token = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the Git binary with the supplied arguments.
    /// </summary>
    /// <param name="arguments">Command-line arguments to pass to Git.</param>
    /// <param name="workingDirectory">Optional working directory.</param>
    /// <param name="env">Optional environment variables.</param>
    /// <param name="log">Whether command execution should be logged.</param>
    /// <param name="cancellationToken">Token that propagates cancellation.</param>
    /// <returns>A task whose result is the collection returned by run.</returns>
    ValueTask<List<string>> Run(string arguments, string? workingDirectory = null, Dictionary<string, string>? env = null, bool log = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Pulls git.
    /// </summary>
    /// <param name="directory">Directory to read from or write to.</param>
    /// <param name="token">Arbitrary utility token to append.</param>
    /// <param name="cancellationToken">Token that propagates cancellation.</param>
    /// <returns>A task that completes when the pull operation is complete.</returns>
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
    /// <returns>A task that completes when the commit operation is complete.</returns>
    ValueTask Commit(string directory, string message, string? name = null, string? email = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pushes the current HEAD of the specified repository to its origin remote.
    /// </summary>
    /// <param name="directory">Repository directory.</param>
    /// <param name="token">Personal access token used for authentication.</param>
    /// <param name="cancellationToken">Token that propagates cancellation.</param>
    /// <returns>A task that completes when the push operation is complete.</returns>
    ValueTask Push(string directory, string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pulls and Push.
    /// </summary>
    /// <param name="directory">Repository directory.</param>
    /// <param name="token">Personal access token used for authentication.</param>
    /// <param name="cancellationToken">Token that propagates cancellation.</param>
    /// <returns>A task that completes when the pull and push operation is complete.</returns>
    ValueTask PullAndPush(string directory, string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds the specified file to the index only if it is not already tracked.
    /// </summary>
    /// <param name="directory">Repository directory.</param>
    /// <param name="relativeOrAbsolutePath">Path to add, relative to the repository root or absolute.</param>
    /// <param name="cancellationToken">Token that propagates cancellation.</param>
    /// <returns>A task that completes when the if not exists addition is complete.</returns>
    ValueTask AddIfNotExists(string directory, string relativeOrAbsolutePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches git.
    /// </summary>
    /// <param name="directory">Directory to read from or write to.</param>
    /// <param name="token">Arbitrary utility token to append.</param>
    /// <param name="cancellationToken">Token that propagates cancellation.</param>
    /// <returns>A task that completes when the fetch operation is complete.</returns>
    ValueTask Fetch(string directory, string? token = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Recursively scans <paramref name="directory"/> and returns every Git repository root found.
    /// </summary>
    /// <param name="directory">Root directory to scan.</param>
    /// <param name="cancellationToken">Token that propagates cancellation.</param>
    /// <returns>A task whose result is the collection returned by get All Git Repositories Recursively.</returns>
    ValueTask<List<string>> GetAllGitRepositoriesRecursively(string directory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the subset of Git repositories under <paramref name="directory"/>
    /// that are currently dirty.
    /// </summary>
    /// <param name="directory">Root directory to scan.</param>
    /// <param name="cancellationToken">Token that propagates cancellation.</param>
    /// <returns>A task whose result is the collection returned by get All Dirty Repositories.</returns>
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
    /// <returns>A task that completes when the commit and push operation is complete.</returns>
    ValueTask CommitAndPush(string directory, string message, string token, string? name = null, string? email = null,
        CancellationToken cancellationToken = default);
}
