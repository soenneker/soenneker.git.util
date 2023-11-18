using System.Collections.Generic;
using System.Threading.Tasks;

namespace Soenneker.Git.Util.Abstract;

/// <summary>
/// A utility library for useful and common Git operations
/// </summary>
public interface IGitUtil
{
    bool IsRepositoryDirty(string directory);

    bool IsRepository(string directory);

    void Pull(string directory);

    void Commit(string directory, string message);

    ValueTask Push(string directory, string username, string token, bool delayOnSuccess = true);

    void Clone(string uri, string directory);

    void AddIfNotExists(string directory, string relativeFilePath);

    List<string> GetAllGitRepositoriesRecursively(string directory);

    /// <summary>
    /// Recursively
    /// </summary>
    void PullAllGitRepositories(string directory);

    /// <summary>
    /// Recursively
    /// </summary>
    void SwitchAllGitRepositoriesToRemoteBranch(string directory);

    /// <summary>
    /// Recursively
    /// </summary>
    void CommitAllRepositories(string directory, string? commitMessage = null);

    /// <summary>
    /// Recursively
    /// </summary>
    void FetchAllGitRepositories(string directory);

    /// <summary>
    /// Recursively
    /// </summary>
    ValueTask PushAllRepositories(string directory, string username, string token, bool delayOnSuccess = true);

    /// <summary>
    /// Dirty = uncommited/unpushed
    /// </summary>
    List<string> GetAllDirtyRepositories(string directory);
}