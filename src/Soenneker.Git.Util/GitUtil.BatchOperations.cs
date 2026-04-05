using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Soenneker.Extensions.Task;
using Soenneker.Extensions.ValueTask;

namespace Soenneker.Git.Util;

public sealed partial class GitUtil
{
    public async ValueTask PullAllGitRepositories(string root, string? token = null, bool parallel = false, CancellationToken cancellationToken = default)
    {
        List<string> repos = await GetAllGitRepositoriesRecursively(root, cancellationToken)
            .NoSync();

        await ForEachRepo(repos, parallel, cancellationToken, (repo, ct) => Pull(repo, token, ct))
            .NoSync();
    }

    public async ValueTask FetchAllGitRepositories(string root, string? token = null, bool parallel = false, CancellationToken cancellationToken = default)
    {
        List<string> repos = await GetAllGitRepositoriesRecursively(root, cancellationToken)
            .NoSync();

        await ForEachRepo(repos, parallel, cancellationToken, (repo, ct) => Fetch(repo, token, ct))
            .NoSync();
    }

    public async ValueTask DeleteMultiPackIndexesForAllRepositories(string root, bool parallel = false, CancellationToken cancellationToken = default)
    {
        List<string> repos = await GetAllGitRepositoriesRecursively(root, cancellationToken)
            .NoSync();

        await ForEachRepo(repos, parallel, cancellationToken, (repo, ct) => DeleteMultiPackIndex(repo, ct))
            .NoSync();
    }

    public async ValueTask RepackIndexesForAllRepositories(string root, bool parallel = false, CancellationToken cancellationToken = default)
    {
        List<string> repos = await GetAllGitRepositoriesRecursively(root, cancellationToken)
            .NoSync();

        await ForEachRepo(repos, parallel, cancellationToken, (repo, ct) => RepackIndexes(repo, ct))
            .NoSync();
    }

    public async ValueTask GarbageCollectAllRepositories(string root, bool parallel = false, CancellationToken cancellationToken = default)
    {
        List<string> repos = await GetAllGitRepositoriesRecursively(root, cancellationToken)
            .NoSync();

        await ForEachRepo(repos, parallel, cancellationToken, (repo, ct) => GarbageCollect(repo, ct))
            .NoSync();
    }

    public async ValueTask GarbageCollectAllRepositoriesOrReclone(string root, string? token = null, bool parallel = false,
        CancellationToken cancellationToken = default)
    {
        List<string> repos = await GetAllGitRepositoriesRecursively(root, cancellationToken)
            .NoSync();

        await ForEachRepo(repos, parallel, cancellationToken, (repo, ct) => GarbageCollectOrReclone(repo, token, ct))
            .NoSync();
    }

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

    public async ValueTask SwitchAllGitRepositoriesToRemoteBranch(string root, string? token = null, bool parallel = false,
        CancellationToken cancellationToken = default)
    {
        List<string> repos = await GetAllGitRepositoriesRecursively(root, cancellationToken)
            .NoSync();

        await ForEachRepo(repos, parallel, cancellationToken, (repo, ct) => SwitchToRemoteBranch(repo, token, ct))
            .NoSync();
    }

    public async ValueTask CommitAllRepositories(string root, string commitMessage, bool parallel = false, CancellationToken cancellationToken = default)
    {
        List<string> repos = await GetAllGitRepositoriesRecursively(root, cancellationToken)
            .NoSync();

        await ForEachRepo(repos, parallel, cancellationToken, (repo, ct) => Commit(repo, commitMessage, null, null, ct))
            .NoSync();
    }

    public async ValueTask PushAllRepositories(string root, string token, bool parallel = false, CancellationToken cancellationToken = default)
    {
        List<string> repos = await GetAllGitRepositoriesRecursively(root, cancellationToken)
            .NoSync();

        await ForEachRepo(repos, parallel, cancellationToken, (repo, ct) => Push(repo, token, ct))
            .NoSync();
    }

    public async ValueTask PullAndPushAllRepositories(string root, string token, bool parallel = false, CancellationToken cancellationToken = default)
    {
        List<string> repos = await GetAllGitRepositoriesRecursively(root, cancellationToken)
            .NoSync();

        await ForEachRepo(repos, parallel, cancellationToken, (repo, ct) => PullAndPush(repo, token, ct))
            .NoSync();
    }
}
