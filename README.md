[![](https://img.shields.io/nuget/v/Soenneker.Git.Util.svg?style=for-the-badge)](https://www.nuget.org/packages/Soenneker.Git.Util/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.git.util/publish-package.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.git.util/actions/workflows/publish-package.yml)
[![](https://img.shields.io/nuget/dt/Soenneker.Git.Util.svg?style=for-the-badge)](https://www.nuget.org/packages/Soenneker.Git.Util/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.git.util/codeql.yml?label=CodeQL&style=for-the-badge)](https://github.com/soenneker/soenneker.git.util/actions/workflows/codeql.yml)

# Soenneker.Git.Util

High-level, asynchronous helpers for working with Git repositories, including discovery, cloning, fetching, pulling, committing, pushing, and bulk repository operations.

## Install

```bash
dotnet add package Soenneker.Git.Util
```

## Quick start

```csharp
using Soenneker.Git.Util.Registrars;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
var result = services.AddGitUtilAsSingleton();
```

Adds `IGitUtil` as a singleton service.

## What you get

- `IGitUtil` — High-level, asynchronous helpers for working with Git repositories, including discovery, cloning, fetching, pulling, committing, pushing, and bulk repository operations.
- `GitUtilRegistrar` — A utility library for useful and common Git operations.
- `GitUtil` — Represents the git util.

## API at a glance

| API | What it does | Result / important behavior |
| --- | --- | --- |
| `IGitUtil.PullAllGitRepositories(root, token, parallel, cancellationToken)` | Pulls all Git Repositories. | A task that completes when the pull all git repositories operation is complete. |
| `IGitUtil.FetchAllGitRepositories(root, token, parallel, cancellationToken)` | Fetches all Git Repositories. | A task that completes when the fetch all git repositories operation is complete. |
| `IGitUtil.DeleteMultiPackIndexesForAllRepositories(root, parallel, cancellationToken)` | Deletes the Git multi-pack-index file for every repository discovered beneath `root`. | Completes when the requested deletion has finished. |
| `IGitUtil.RepackIndexesForAllRepositories(root, parallel, cancellationToken)` | Rebuilds the Git multi-pack-index for every repository discovered beneath `root`. | A task that completes when the repack indexes for all repositories operation is complete. |
| `IGitUtil.GarbageCollectAllRepositories(root, parallel, cancellationToken)` | Runs Git garbage collection for every repository discovered beneath `root`. | A task that completes when the garbage collect all repositories operation is complete. |
| `IGitUtil.GarbageCollectAllRepositoriesOrReclone(root, token, parallel, cancellationToken)` | Garbage-collects all Repositories Or Reclone. | A task that completes when the garbage collect all repositories or reclone operation is complete. |
| `IGitUtil.IntegrityCheckAllRepositories(root, parallel, cancellationToken)` | Runs a full Git integrity check for every repository discovered beneath `root`. | A task that completes when the integrity check all repositories operation is complete. |
| `IGitUtil.SwitchAllGitRepositoriesToRemoteBranch(root, token, parallel, cancellationToken)` | Switches all Git Repositories To Remote Branch. | A task that completes when the switch all git repositories to remote branch operation is complete. |
| `IGitUtil.CommitAllRepositories(root, commitMessage, parallel, cancellationToken)` | Commits all repositories discovered beneath `root` using the supplied commit message. | A task that completes when the commit all repositories operation is complete. |
| `IGitUtil.PushAllRepositories(root, token, parallel, cancellationToken)` | Pushes every Git repository discovered beneath `root`. | A task that completes when the push all repositories operation is complete. |
| `IGitUtil.PullAndPushAllRepositories(root, token, parallel, cancellationToken)` | For every Git repository discovered beneath `root`, performs a pull and then a push. | A task that completes when the pull and push all repositories operation is complete. |
| `IGitUtil.SwitchToRemoteBranch(directory, token, cancellationToken)` | Switches to Remote Branch. | A task that completes when the switch to remote branch operation is complete. |
| `IGitUtil.GarbageCollectOrReclone(directory, token, cancellationToken)` | Garbage-collects or Reclone. | A task that completes when the garbage collect or reclone operation is complete. |
| `IGitUtil.IsRepository(directory, cancellationToken)` | Determines whether the specified directory contains a Git working tree. | true if the specified directory contains a Git working tree; otherwise, false. |
| `IGitUtil.IsRepositoryDirty(directory, cancellationToken)` | Determines whether the repository has local working tree changes or has diverged from its upstream. | true if the repository has local working tree changes or has diverged from its upstream; otherwise, false. |
| `IGitUtil.HasStagedChanges(directory, cancellationToken)` | Determines whether the repository has staged changes. | true if the repository has staged changes; otherwise, false. |
| `IGitUtil.HasWorkingTreeChanges(directory, cancellationToken)` | Determines whether the repository has working tree changes. | true if the repository has working tree changes; otherwise, false. |
| `IGitUtil.Clone(uri, directory, token, shallow, cancellationToken)` | Clones git. | A task that completes when the clone operation is complete. |

## Practical notes

- Cancellation stops pending work; it does not undo work that has already completed.
