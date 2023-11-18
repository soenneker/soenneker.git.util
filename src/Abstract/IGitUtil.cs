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

    void Push(string directory, string username, string token);

    void Clone(string uri, string directory);

    void AddIfNotExists(string directory, string relativeFilePath);
}