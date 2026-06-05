using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Soenneker.Extensions.Task;
using Soenneker.Extensions.ValueTask;

namespace Soenneker.Git.Util;

/// <summary>
/// Represents the git util.
/// </summary>
public sealed partial class GitUtil
{
    /// <summary>
    /// Executes the pull all git repositories operation.
    /// </summary>
    /// <param name="root">The root.</param>
    /// <param name="token">The token.</param>
    /// <param name="parallel">The parallel.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async ValueTask PullAllGitRepositories(string root, string? token = null, bool parallel = false, CancellationToken cancellationToken = default)
    {
        List<string> repos = await GetAllGitRepositoriesRecursively(root, cancellationToken)
            .NoSync();

        await ForEachRepo(repos, parallel, cancellationToken, (repo, ct) => Pull(repo, token, ct))
            .NoSync();
    }

    /// <summary>
    /// Executes the fetch all git repositories operation.
    /// </summary>
    /// <param name="root">The root.</param>
    /// <param name="token">The token.</param>
    /// <param name="parallel">The parallel.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async ValueTask FetchAllGitRepositories(string root, string? token = null, bool parallel = false, CancellationToken cancellationToken = default)
    {
        List<string> repos = await GetAllGitRepositoriesRecursively(root, cancellationToken)
            .NoSync();

        await ForEachRepo(repos, parallel, cancellationToken, (repo, ct) => Fetch(repo, token, ct))
            .NoSync();
    }

    /// <summary>
    /// Deletes multi pack indexes for all repositories.
    /// </summary>
    /// <param name="root">The root.</param>
    /// <param name="parallel">The parallel.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async ValueTask DeleteMultiPackIndexesForAllRepositories(string root, bool parallel = false, CancellationToken cancellationToken = default)
    {
        List<string> repos = await GetAllGitRepositoriesRecursively(root, cancellationToken)
            .NoSync();

        await ForEachRepo(repos, parallel, cancellationToken, (repo, ct) => DeleteMultiPackIndex(repo, ct))
            .NoSync();
    }

    /// <summary>
    /// Executes the repack indexes for all repositories operation.
    /// </summary>
    /// <param name="root">The root.</param>
    /// <param name="parallel">The parallel.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async ValueTask RepackIndexesForAllRepositories(string root, bool parallel = false, CancellationToken cancellationToken = default)
    {
        List<string> repos = await GetAllGitRepositoriesRecursively(root, cancellationToken)
            .NoSync();

        await ForEachRepo(repos, parallel, cancellationToken, (repo, ct) => RepackIndexes(repo, ct))
            .NoSync();
    }

    /// <summary>
    /// Executes the garbage collect all repositories operation.
    /// </summary>
    /// <param name="root">The root.</param>
    /// <param name="parallel">The parallel.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async ValueTask GarbageCollectAllRepositories(string root, bool parallel = false, CancellationToken cancellationToken = default)
    {
        List<string> repos = await GetAllGitRepositoriesRecursively(root, cancellationToken)
            .NoSync();

        await ForEachRepo(repos, parallel, cancellationToken, (repo, ct) => GarbageCollect(repo, ct))
            .NoSync();
    }

    /// <summary>
    /// Executes the garbage collect all repositories or reclone operation.
    /// </summary>
    /// <param name="root">The root.</param>
    /// <param name="token">The token.</param>
    /// <param name="parallel">The parallel.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async ValueTask GarbageCollectAllRepositoriesOrReclone(string root, string? token = null, bool parallel = false,
        CancellationToken cancellationToken = default)
    {
        List<string> repos = await GetAllGitRepositoriesRecursively(root, cancellationToken)
            .NoSync();

        await ForEachRepo(repos, parallel, cancellationToken, (repo, ct) => GarbageCollectOrReclone(repo, token, ct))
            .NoSync();
    }

    /// <summary>
    /// Executes the integrity check all repositories operation.
    /// </summary>
    /// <param name="root">The root.</param>
    /// <param name="parallel">The parallel.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async ValueTask IntegrityCheckAllRepositories(string root, bool parallel = false, CancellationToken cancellationToken = default)
    {
        List<string> repos = await GetAllGitRepositoriesRecursively(root, cancellationToken)
            .NoSync();

        await ForEachRepo(repos, parallel, cancellationToken, async (repo, ct) =>
            {
                await IntegrityCheck(repo, ct)
                    .NoSync();
            })
            .NoSync();
    }

    /// <summary>
    /// Executes the switch all git repositories to remote branch operation.
    /// </summary>
    /// <param name="root">The root.</param>
    /// <param name="token">The token.</param>
    /// <param name="parallel">The parallel.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async ValueTask SwitchAllGitRepositoriesToRemoteBranch(string root, string? token = null, bool parallel = false,
        CancellationToken cancellationToken = default)
    {
        List<string> repos = await GetAllGitRepositoriesRecursively(root, cancellationToken)
            .NoSync();

        await ForEachRepo(repos, parallel, cancellationToken, (repo, ct) => SwitchToRemoteBranch(repo, token, ct))
            .NoSync();
    }

    /// <summary>
    /// Executes the commit all repositories operation.
    /// </summary>
    /// <param name="root">The root.</param>
    /// <param name="commitMessage">The commit message.</param>
    /// <param name="parallel">The parallel.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async ValueTask CommitAllRepositories(string root, string commitMessage, bool parallel = false, CancellationToken cancellationToken = default)
    {
        List<string> repos = await GetAllGitRepositoriesRecursively(root, cancellationToken)
            .NoSync();

        await ForEachRepo(repos, parallel, cancellationToken, (repo, ct) => Commit(repo, commitMessage, null, null, ct))
            .NoSync();
    }

    /// <summary>
    /// Executes the push all repositories operation.
    /// </summary>
    /// <param name="root">The root.</param>
    /// <param name="token">The token.</param>
    /// <param name="parallel">The parallel.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async ValueTask PushAllRepositories(string root, string token, bool parallel = false, CancellationToken cancellationToken = default)
    {
        List<string> repos = await GetAllGitRepositoriesRecursively(root, cancellationToken)
            .NoSync();

        await ForEachRepo(repos, parallel, cancellationToken, (repo, ct) => Push(repo, token, ct))
            .NoSync();
    }

    /// <summary>
    /// Executes the pull and push all repositories operation.
    /// </summary>
    /// <param name="root">The root.</param>
    /// <param name="token">The token.</param>
    /// <param name="parallel">The parallel.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async ValueTask PullAndPushAllRepositories(string root, string token, bool parallel = false, CancellationToken cancellationToken = default)
    {
        List<string> repos = await GetAllGitRepositoriesRecursively(root, cancellationToken)
            .NoSync();

        await ForEachRepo(repos, parallel, cancellationToken, (repo, ct) => PullAndPush(repo, token, ct))
            .NoSync();
    }
}
