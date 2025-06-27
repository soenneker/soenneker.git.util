using System.Collections.Generic;
using System.Threading.Tasks;
using AwesomeAssertions;
using Soenneker.Facts.Local;
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

    [LocalFact]
    public async ValueTask GetAllGitRepositoriesRecursively_should_not_be_null_or_empty()
    {
        List<string> result = await _util.GetAllGitRepositoriesRecursively(@"");
        result.Should().NotBeNullOrEmpty();
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