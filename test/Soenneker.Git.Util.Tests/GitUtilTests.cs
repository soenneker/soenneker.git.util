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

    [ManualFact]
    //[LocalFact]
    public void GetAllGitRepositoriesRecursively_should_not_be_null_or_empty()
    {
        List<string> result = _util.GetAllGitRepositoriesRecursively(@"c:\git");
        result.Should()
              .NotBeNullOrEmpty();
    }

    [LocalFact]
    public async ValueTask CloneToTempDirectory()
    {
        await _util.CloneToTempDirectory("https://github.com/git/git");
    }

    [LocalFact]
    public void Fetch_should_fetch()
    {
        _util.Fetch(@"");
    }

    [LocalFact]
    public void Pull_should_pull()
    {
        _util.Pull(@"");
    }
}