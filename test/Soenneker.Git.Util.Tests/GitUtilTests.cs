using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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

    [Test]
    public async ValueTask GetAllDirtyRepositories_should_return_dirty_repositories()
    {
        string root = Directory.CreateTempSubdirectory().FullName;
        string repo = Path.Join(root, "repo");
        Directory.CreateDirectory(repo);

        try
        {
            await RunGit("init", repo);
            await File.WriteAllTextAsync(Path.Join(repo, "dirty.txt"), "dirty");

            List<string> result = await _util.GetAllDirtyRepositories(root);

            result.Should()
                  .ContainSingle()
                  .Which.Should()
                  .Be(repo);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Test]
    public async ValueTask GetAllDirtyRepositories_should_return_dirty_repository_when_directory_is_repo_root()
    {
        string repo = Directory.CreateTempSubdirectory().FullName;

        try
        {
            await RunGit("init", repo);
            await File.WriteAllTextAsync(Path.Join(repo, "dirty.txt"), "dirty");

            List<string> result = await _util.GetAllDirtyRepositories(repo);

            result.Should()
                  .ContainSingle()
                  .Which.Should()
                  .Be(repo);
        }
        finally
        {
            DeleteDirectory(repo);
        }
    }

    [Test]
    public async ValueTask GetAllDirtyRepositories_should_return_repository_with_unpushed_commit()
    {
        string root = Directory.CreateTempSubdirectory().FullName;
        string remote = Path.Join(root, "remote.git");
        string repo = Path.Join(root, "repo");

        try
        {
            await RunGit("init --bare remote.git", root);
            await RunGit($"clone \"{remote}\" repo", root);
            await ConfigureGitUser(repo);

            await File.WriteAllTextAsync(Path.Join(repo, "pushed.txt"), "pushed");
            await RunGit("add pushed.txt", repo);
            await RunGit("commit -m pushed", repo);
            await RunGit("push -u origin HEAD", repo);

            await File.WriteAllTextAsync(Path.Join(repo, "unpushed.txt"), "unpushed");
            await RunGit("add unpushed.txt", repo);
            await RunGit("commit -m unpushed", repo);

            List<string> result = await _util.GetAllDirtyRepositories(root);

            result.Should()
                  .Contain(repo);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

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

    private static async ValueTask RunGit(string arguments, string workingDirectory)
    {
        using var process = Process.Start(new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        })!;

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
            throw new IOException(await process.StandardError.ReadToEndAsync());
    }

    private static async ValueTask ConfigureGitUser(string workingDirectory)
    {
        await RunGit("config user.name Test", workingDirectory);
        await RunGit("config user.email example@example.com", workingDirectory);
    }

    private static void DeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
            return;

        foreach (string file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);

        Directory.Delete(path, true);
    }
}
