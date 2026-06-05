using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Soenneker.Extensions.String;
using Soenneker.Extensions.ValueTask;

namespace Soenneker.Git.Util;

/// <summary>
/// Represents the git util.
/// </summary>
public sealed partial class GitUtil
{
    /// <summary>
    /// Deletes multi pack index.
    /// </summary>
    /// <param name="directory">The directory.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async ValueTask DeleteMultiPackIndex(string directory, CancellationToken cancellationToken = default)
    {
        string multiPackIndexPath = Path.Join(directory, ".git", "objects", "pack", "multi-pack-index");

        await _fileUtil.Delete(multiPackIndexPath, ignoreMissing: true, cancellationToken: cancellationToken)
                       .NoSync();
    }

    /// <summary>
    /// Executes the repack indexes operation.
    /// </summary>
    /// <param name="directory">The directory.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async ValueTask RepackIndexes(string directory, CancellationToken cancellationToken = default)
    {
        try
        {
            await Run("repack -a -d", directory, cancellationToken: cancellationToken)
                .NoSync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not repack indexes for {Dir}", directory);
        }
    }

    /// <summary>
    /// Executes the garbage collect operation.
    /// </summary>
    /// <param name="directory">The directory.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async ValueTask GarbageCollect(string directory, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Garbage collecting {Dir}...", directory);

            await Run("gc", directory, cancellationToken: cancellationToken)
                .NoSync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not garbage collect {Dir}", directory);
        }
    }

    /// <summary>
    /// Executes the garbage collect or reclone operation.
    /// </summary>
    /// <param name="directory">The directory.</param>
    /// <param name="token">The token.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async ValueTask GarbageCollectOrReclone(string directory, string? token = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Garbage collecting {Dir}...", directory);

        try
        {
            await Run("gc", directory, cancellationToken: cancellationToken)
                .NoSync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Garbage collection failed for {Dir}; deleting and re-cloning from origin...", directory);

            try
            {
                List<string> originOutput = await Run("remote get-url origin", directory, log: false, cancellationToken: cancellationToken)
                    .NoSync();

                if (originOutput.Count == 0 || originOutput[0].IsNullOrWhiteSpace())
                    throw new InvalidOperationException($"Could not determine origin URL for {directory}.");

                string originUrl = originOutput[0].Trim();

                await _directoryUtil.Delete(directory, cancellationToken)
                    .NoSync();

                await Clone(originUrl, directory, token, cancellationToken: cancellationToken)
                    .NoSync();
            }
            catch (Exception recloneEx)
            {
                _logger.LogError(recloneEx, "Could not recover {Dir} by re-cloning after garbage collection failure", directory);
            }
        }
    }

    /// <summary>
    /// Executes the integrity check operation.
    /// </summary>
    /// <param name="directory">The directory.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task containing the result of the operation.</returns>
    public async ValueTask<bool> IntegrityCheck(string directory, CancellationToken cancellationToken = default)
    {
        try
        {
            await Run("fsck --full", directory, cancellationToken: cancellationToken)
                .NoSync();

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not integrity check {Dir}", directory);
            return false;
        }
    }

    /// <summary>
    /// Executes the pull and push operation.
    /// </summary>
    /// <param name="directory">The directory.</param>
    /// <param name="token">The token.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async ValueTask PullAndPush(string directory, string token, CancellationToken cancellationToken = default)
    {
        await Pull(directory, token, cancellationToken)
            .NoSync();

        await Push(directory, token, cancellationToken)
            .NoSync();
    }
}
