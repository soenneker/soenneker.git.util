using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AwesomeAssertions;
using Soenneker.Git.Util.Abstract;
using Soenneker.Tests.HostedUnit;

namespace Soenneker.Git.Util.Tests;

[ClassDataSource<Host>(Shared = SharedType.PerTestSession)]
public class GitUtilParallelismTests : HostedUnitTest
{
    private readonly IGitUtil _util;

    public GitUtilParallelismTests(Host host) : base(host)
    {
        _util = Resolve<IGitUtil>(true);
    }

    [Test]
    [Arguments(0)]
    [Arguments(-1)]
    public async Task Methods_should_reject_invalid_parallelism_before_discovery(int maxParallelism)
    {
        // Empty roots must still validate the limit, before returning or starting Git.
        List<Func<Task>> operations =
        [
            async () => { await _util.GetAllGitRepositoriesRecursively("", maxParallelism: maxParallelism); },
            async () => { await _util.GetAllDirtyRepositories("", maxParallelism: maxParallelism); },
            async () => await _util.PullAllGitRepositories("", maxParallelism: maxParallelism),
            async () => await _util.FetchAllGitRepositories("", maxParallelism: maxParallelism),
            async () => await _util.DeleteMultiPackIndexesForAllRepositories("", maxParallelism: maxParallelism),
            async () => await _util.RepackIndexesForAllRepositories("", maxParallelism: maxParallelism),
            async () => await _util.GarbageCollectAllRepositories("", maxParallelism: maxParallelism),
            async () => await _util.GarbageCollectAllRepositoriesOrReclone("", maxParallelism: maxParallelism),
            async () => await _util.IntegrityCheckAllRepositories("", maxParallelism: maxParallelism),
            async () => await _util.SwitchAllGitRepositoriesToRemoteBranch("", maxParallelism: maxParallelism),
            async () => await _util.CommitAllRepositories("", "test", maxParallelism: maxParallelism),
            async () => await _util.PushAllRepositories("", "", maxParallelism: maxParallelism),
            async () => await _util.PullAndPushAllRepositories("", "", maxParallelism: maxParallelism)
        ];

        foreach (Func<Task> operation in operations)
            await operation.Should().ThrowAsync<ArgumentOutOfRangeException>().WithParameterName("maxParallelism");
    }
}
