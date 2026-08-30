[![](https://img.shields.io/nuget/v/Soenneker.Git.Util.svg?style=for-the-badge)](https://www.nuget.org/packages/Soenneker.Git.Util/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.git.util/publish-package.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.git.util/actions/workflows/publish-package.yml)
[![](https://img.shields.io/nuget/dt/Soenneker.Git.Util.svg?style=for-the-badge)](https://www.nuget.org/packages/Soenneker.Git.Util/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.git.util/codeql.yml?label=CodeQL&style=for-the-badge)](https://github.com/soenneker/soenneker.git.util/actions/workflows/codeql.yml)

# Soenneker.Git.Util

Asynchronous Git operations backed by bundled Windows and Linux Git distributions, with repository discovery and optional bounded parallel execution across multiple repositories.

## Install

```bash
dotnet add package Soenneker.Git.Util
```

## Configure and register

```csharp
using Soenneker.Git.Util.Registrars;
using Microsoft.Extensions.DependencyInjection;

services.AddGitUtilAsSingleton();
```

Scoped registration is available through `AddGitUtilAsScoped()`.

The utility reads its default credentials and branch once when it is constructed:

```json
{
  "Git": {
    "Token": "<GitHub token>",
    "Name": "Commit author",
    "Email": "author@example.com",
    "DefaultBranch": "main",
    "Log": false
  }
}
```

`Token`, `Name`, and `Email` are required configuration values. A token supplied to an individual method overrides the configured token. Authentication is non-interactive and is applied to GitHub HTTPS requests without putting the token in the remote URL or command log.

## Common operations

```csharp
IGitUtil git = serviceProvider.GetRequiredService<IGitUtil>();

string checkout = await git.CloneToTempDirectory(
    "https://github.com/example/project.git",
    cancellationToken: cancellationToken);

await git.Fetch(checkout, cancellationToken: cancellationToken);

if (await git.HasWorkingTreeChanges(checkout, cancellationToken))
{
    await git.Commit(checkout, "Update generated files", cancellationToken: cancellationToken);
    await git.Push(checkout, token, cancellationToken);
}
```

`CloneToTempDirectory` returns a shallow clone and transfers ownership of that temporary directory to the caller.

## Repository discovery and batches

```csharp
IReadOnlyList<string> dirty =
    await git.GetAllDirtyRepositories(@"C:\git", cancellationToken);

await git.FetchAllGitRepositories(
    @"C:\git",
    parallel: true,
    cancellationToken: cancellationToken);
```

Discovery accepts either a repository root or a directory containing repositories. Batch operations are sequential by default. With `parallel: true`, concurrency is bounded to at most eight repositories and the processor count.

## API at a glance

| API | What it does | Result / important behavior |
| --- | --- | --- |
| `Clone`, `CloneToTempDirectory` | Clone a repository, optionally with blob filtering and depth one. | Temporary clones must be deleted by the caller. |
| `Fetch`, `Pull`, `Push`, `PullAndPush` | Exchange commits with `origin` using the configured branch. | Failures propagate to the caller. |
| `Commit`, `CommitAndPush` | Stage all changes and create a commit when anything is staged. | Uses explicit identity values or the configured defaults. |
| `IsRepository`, `IsRepositoryDirty`, `HasStagedChanges`, `HasWorkingTreeChanges` | Inspect repository state. | `IsRepositoryDirty` also detects upstream divergence. |
| `GetAllGitRepositoriesRecursively`, `GetAllDirtyRepositories` | Find repository roots beneath a directory. | Also recognizes worktree and submodule `.git` files. |
| `*AllRepositories` methods | Apply fetch, pull, commit, push, integrity, or maintenance operations to discovered repositories. | Can run sequentially or with bounded parallelism. |
| `Run` | Execute arbitrary arguments with the bundled Git binary. | This is a raw escape hatch; only pass trusted arguments. |

## Synchronization and recovery safety

`SwitchToRemoteBranch` fetches `origin`, then checks out the configured branch only when the working tree is clean and local `HEAD` is an ancestor of the remote branch. It refuses uncommitted changes, unpushed commits, and divergent history instead of resetting or cleaning them.

`GarbageCollectOrReclone` is intended for repository recovery. If garbage collection fails, it clones a replacement beside the repository first, swaps it into place only after cloning succeeds, and restores the original directory if the swap fails. A successful replacement intentionally discards local-only repository state, so use this method only when recloning from `origin` is acceptable.

## Practical notes

- Cancellation stops process execution and pending batch work; it cannot undo a Git operation that already completed.
- `DeleteMultiPackIndex`, repack, garbage collection, commits, pushes, and recovery methods mutate repositories. Use the inspection methods first when caller-owned work may be present.
