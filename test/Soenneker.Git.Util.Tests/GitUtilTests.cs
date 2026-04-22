using System.Collections.Generic;
using System.Threading.Tasks;
using AwesomeAssertions;
using Soenneker.Tests.Attributes.Local;
using Soenneker.Tests.HostedUnit;
using Soenneker.Git.Util.Abstract;

namespace Soenneker.Git.Util.Tests;

[ClassDataSource<Host>(Shared = SharedType.PerTestSession)]
public class GitUtilTests : HostedUnitTest
{
    private readonly IGitUtil _util;

    public GitUtilTests(Host host) : base(host)
    {
        _util = Resolve<IGitUtil>(true);
    }

    [Test]
    public void Default()
    { }

    [Skip("Manual")]
    //[LocalOnly]
    public async ValueTask GetAllGitRepositoriesRecursively_should_not_be_null_or_empty()
    {
        List<string> result = await _util.GetAllGitRepositoriesRecursively(@"c:\git");
        result.Should()
              .NotBeNullOrEmpty();
    }

    [LocalOnly]
    public async ValueTask CloneToTempDirectory()
    {
        await _util.CloneToTempDirectory("https://github.com/git/git");
    }

    [LocalOnly]
    public async ValueTask Fetch_should_fetch()
    {
        await _util.Fetch(@"");
    }

    [LocalOnly]
    public async ValueTask Pull_should_pull()
    {
        await _util.Pull(@"");
    }
}
