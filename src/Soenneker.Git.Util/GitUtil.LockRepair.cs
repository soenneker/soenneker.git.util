using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Soenneker.Extensions.ValueTask;

namespace Soenneker.Git.Util;

public sealed partial class GitUtil
{
    public async ValueTask<int> DeleteStaleIndexLocksForAllRepositories(string root, CancellationToken cancellationToken = default)
    {
        List<string> repos = await GetAllGitRepositoriesRecursively(root, cancellationToken).NoSync();
        int removed = 0;

        foreach (string repo in repos)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // Ask Git so linked worktrees and repositories with a .git file resolve correctly.
                List<string> output = await Run("rev-parse --path-format=absolute --git-path index.lock", repo, log: false,
                    cancellationToken: cancellationToken).NoSync();
                if (output.Count != 1 || !Path.IsPathFullyQualified(output[0]))
                    throw new InvalidOperationException("Git did not return an absolute index lock path.");

                string path = output[0];
                if (!File.Exists(path))
                    continue;

                if (HasRunningGitProcess())
                {
                    _logger.LogWarning("Skipping index lock cleanup in {Dir} because Git may still be running", repo);
                    continue;
                }

                cancellationToken.ThrowIfCancellationRequested();
                // Never delete a lock that another process has open. Delete this opened file on close.
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose))
                {
                }

                removed++;
                _logger.LogInformation("Removed stale index lock {Path}", path);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not clean up stale index lock in {Dir}", repo);
            }
        }

        return removed;
    }

    private static bool HasRunningGitProcess()
    {
        Process[] processes = Process.GetProcesses();
        try
        {
            foreach (Process process in processes)
            {
                try
                {
                    string name = process.ProcessName;
                    if (name.Equals("git", StringComparison.OrdinalIgnoreCase) || name.StartsWith("git-", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                catch (InvalidOperationException)
                {
                    // A process exited during enumeration.
                }
            }

            return false;
        }
        finally
        {
            foreach (Process process in processes)
                process.Dispose();
        }
    }
}
