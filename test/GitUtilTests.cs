using FluentAssertions;
using Soenneker.Facts.Local;
using Soenneker.Tests.FixturedUnit;
using Soenneker.Git.Util.Abstract;
using Xunit;
using Xunit.Abstractions;

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
    public void GetAllGitRepositoriesRecursively_should_not_be_null_or_empty()
    {
        var result = _util.GetAllGitRepositoriesRecursively(@"c:\git");
        result.Should().NotBeNullOrEmpty();
    }
}