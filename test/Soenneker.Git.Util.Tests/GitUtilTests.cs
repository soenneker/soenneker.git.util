using System.Collections.Generic;
using System.Threading.Tasks;
using AwesomeAssertions;
using Soenneker.Facts.Local;
using Soenneker.Facts.Manual;
using Soenneker.Tests.FixturedUnit;
using Soenneker.Git.Util.Abstract;
using Xunit;

namespace Soenneker.Git.Util.Tests;

[Collection("Collection")]
public class GitUtilTests : FixturedUnitTest
{
    private readonly IGitUtil _util;

    public GitUtilTests(Fixture fixture, ITestOutputHelper output) : base(fixture, output)
    {
        _util = Resolve<IGitUtil>(true);
    }

    [Fact]
    public void Default()
    { }

    [ManualFact]
    //[LocalFact]
    public async ValueTask GetAllGitRepositoriesRecursively_should_not_be_null_or_empty()
    {
        List<string> result = await _util.GetAllGitRepositoriesRecursively(@"c:\git");
        result.Should()
              .NotBeNullOrEmpty();
    }

    [LocalFact]
    public async ValueTask CloneToTempDirectory()
    {
        await _util.CloneToTempDirectory("https://github.com/git/git");
    }

    [LocalFact]
    public async ValueTask Fetch_should_fetch()
    {
        await _util.Fetch(@"");
    }

    [LocalFact]
    public async ValueTask Pull_should_pull()
    {
        await _util.Pull(@"");
    }
}