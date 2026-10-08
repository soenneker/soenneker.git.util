using Soenneker.Utils.File.Abstract;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Soenneker.Tests.Attributes.Local;
using Soenneker.Tests.HostedUnit;
using Soenneker.Git.Util.Abstract;

namespace Soenneker.Git.Util.Tests;

[ClassDataSource<Host>(Shared = SharedType.PerTestSession)]
public class GitUtilTests : HostedUnitTest
{
    private readonly IFileUtil _fileUtil;

    private readonly IGitUtil _util;

    public GitUtilTests(Host host) : base(host)
    {
        _fileUtil = Resolve<IFileUtil>(true);
        _util = Resolve<IGitUtil>(true);
    }

    [Test]
    public void Default()
    { }

    [Test]
    public async ValueTask GetAllDirtyRepositories_should_return_dirty_repositories(CancellationToken cancellationToken)
    {
        string root = Directory.CreateTempSubdirectory().FullName;
        string repo = Path.Join(root, "repo");
        Directory.CreateDirectory(repo);

        try
        {
            await RunGit("init", repo, cancellationToken: cancellationToken);
            await _fileUtil.Write(Path.Join(repo, "dirty.txt"), "dirty", cancellationToken: cancellationToken);

            List<string> result = await _util.GetAllDirtyRepositories(root, cancellationToken: cancellationToken);

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
    public async ValueTask GetAllDirtyRepositories_should_return_dirty_repository_when_directory_is_repo_root(CancellationToken cancellationToken)
    {
        string repo = Directory.CreateTempSubdirectory().FullName;

        try
        {
            await RunGit("init", repo, cancellationToken: cancellationToken);
            await _fileUtil.Write(Path.Join(repo, "dirty.txt"), "dirty", cancellationToken: cancellationToken);

            List<string> result = await _util.GetAllDirtyRepositories(repo, cancellationToken: cancellationToken);

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
    public async ValueTask GetAllDirtyRepositories_should_return_repository_with_unpushed_commit(CancellationToken cancellationToken)
    {
        string root = Directory.CreateTempSubdirectory().FullName;
        string remote = Path.Join(root, "remote.git");
        string repo = Path.Join(root, "repo");

        try
        {
            await RunGit("init --bare remote.git", root, cancellationToken: cancellationToken);
            await RunGit($"clone \"{remote}\" repo", root, cancellationToken: cancellationToken);
            await ConfigureGitUser(repo, cancellationToken: cancellationToken);

            await _fileUtil.Write(Path.Join(repo, "pushed.txt"), "pushed", cancellationToken: cancellationToken);
            await RunGit("add pushed.txt", repo, cancellationToken: cancellationToken);
            await RunGit("commit -m pushed", repo, cancellationToken: cancellationToken);
            await RunGit("push -u origin HEAD", repo, cancellationToken: cancellationToken);

            await _fileUtil.Write(Path.Join(repo, "unpushed.txt"), "unpushed", cancellationToken: cancellationToken);
            await RunGit("add unpushed.txt", repo, cancellationToken: cancellationToken);
            await RunGit("commit -m unpushed", repo, cancellationToken: cancellationToken);

            List<string> result = await _util.GetAllDirtyRepositories(root, cancellationToken: cancellationToken);

            result.Should()
                  .Contain(repo);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Test]
    public async ValueTask GetAllDirtyRepositories_should_detect_behind_repository_but_not_clean_repository(CancellationToken cancellationToken)
    {
        string root = Directory.CreateTempSubdirectory().FullName;
        string remote = Path.Join(root, "remote.git");
        string repo = Path.Join(root, "repo");
        string updater = Path.Join(root, "updater");

        try
        {
            await RunGit("init --bare --initial-branch=main remote.git", root, cancellationToken: cancellationToken);
            await RunGit($"clone \"{remote}\" repo", root, cancellationToken: cancellationToken);
            await ConfigureGitUser(repo, cancellationToken: cancellationToken);
            await _fileUtil.Write(Path.Join(repo, "initial.txt"), "initial", cancellationToken: cancellationToken);
            await RunGit("add initial.txt", repo, cancellationToken: cancellationToken);
            await RunGit("commit -m initial", repo, cancellationToken: cancellationToken);
            await RunGit("push -u origin main", repo, cancellationToken: cancellationToken);

            (await _util.GetAllDirtyRepositories(root, cancellationToken)).Should().BeEmpty();

            await RunGit($"clone \"{remote}\" updater", root, cancellationToken: cancellationToken);
            await ConfigureGitUser(updater, cancellationToken: cancellationToken);
            await _fileUtil.Write(Path.Join(updater, "update.txt"), "update", cancellationToken: cancellationToken);
            await RunGit("add update.txt", updater, cancellationToken: cancellationToken);
            await RunGit("commit -m update", updater, cancellationToken: cancellationToken);
            await RunGit("push", updater, cancellationToken: cancellationToken);
            await RunGit("fetch", repo, cancellationToken: cancellationToken);

            List<string> result = await _util.GetAllDirtyRepositories(root, cancellationToken);

            result.Should().ContainSingle().Which.Should().Be(repo);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Test]
    public async ValueTask SwitchToRemoteBranch_should_not_discard_working_tree_changes(CancellationToken cancellationToken)
    {
        string root = Directory.CreateTempSubdirectory().FullName;
        string remote = Path.Join(root, "remote.git");
        string repo = Path.Join(root, "repo");

        try
        {
            await RunGit("init --bare --initial-branch=main remote.git", root, cancellationToken: cancellationToken);
            await RunGit($"clone \"{remote}\" repo", root, cancellationToken: cancellationToken);
            await ConfigureGitUser(repo, cancellationToken: cancellationToken);
            await _fileUtil.Write(Path.Join(repo, "tracked.txt"), "committed", cancellationToken: cancellationToken);
            await RunGit("add tracked.txt", repo, cancellationToken: cancellationToken);
            await RunGit("commit -m initial", repo, cancellationToken: cancellationToken);
            await RunGit("push -u origin main", repo, cancellationToken: cancellationToken);
            await _fileUtil.Write(Path.Join(repo, "tracked.txt"), "local change", cancellationToken: cancellationToken);

            Func<Task> act = async () => await _util.SwitchToRemoteBranch(repo, cancellationToken: cancellationToken);

            await act.Should().ThrowAsync<InvalidOperationException>();
            (await _fileUtil.Read(Path.Join(repo, "tracked.txt"), cancellationToken: cancellationToken)).Should().Be("local change");
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Test]
    public async ValueTask GetAllGitRepositoriesRecursively_should_find_nested_and_linked_worktrees(CancellationToken cancellationToken)
    {
        string root = Directory.CreateTempSubdirectory().FullName;
        string repo = Path.Join(root, "repo");
        string nestedRepo = Path.Join(repo, "nested");
        string linkedWorktree = Path.Join(root, "linked-worktree");
        string notARepo = Path.Join(root, "not-a-repo");

        Directory.CreateDirectory(nestedRepo);
        Directory.CreateDirectory(linkedWorktree);
        Directory.CreateDirectory(notARepo);

        try
        {
            await RunGit("init", repo, cancellationToken: cancellationToken);
            await RunGit("init", nestedRepo, cancellationToken: cancellationToken);
            await _fileUtil.Write(Path.Join(linkedWorktree, ".git"), "gitdir: ../repo/.git/worktrees/linked-worktree", cancellationToken: cancellationToken);
            await _fileUtil.Write(Path.Join(notARepo, ".git"), "ordinary file", cancellationToken: cancellationToken);

            List<string> result = await _util.GetAllGitRepositoriesRecursively(root, cancellationToken);

            result.Should().BeEquivalentTo([repo, nestedRepo, linkedWorktree]);
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

    private static async ValueTask RunGit(string arguments, string workingDirectory, CancellationToken cancellationToken = default)
    {
        using Process process = Process.Start(new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        })!;

        await process.WaitForExitAsync(cancellationToken: cancellationToken);

        if (process.ExitCode != 0)
            throw new IOException(await process.StandardError.ReadToEndAsync(cancellationToken: cancellationToken));
    }

    private static async ValueTask ConfigureGitUser(string workingDirectory, CancellationToken cancellationToken = default)
    {
        await RunGit("config user.name Test", workingDirectory, cancellationToken: cancellationToken);
        await RunGit("config user.email example@example.com", workingDirectory, cancellationToken: cancellationToken);
    }

    private async Task DeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
            return;

        foreach (string file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);

        Directory.Delete(path, true);
    }
}
