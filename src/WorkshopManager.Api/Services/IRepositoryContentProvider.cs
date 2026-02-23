namespace WorkshopManager.Services;

/// <summary>
/// Abstraction for reading repository contents. Backed by GitHub API in production,
/// in-memory or filesystem in tests.
/// </summary>
public interface IRepositoryContentProvider
{
    /// <summary>
    /// Get the repository file tree (paths only, no content).
    /// </summary>
    Task<IReadOnlyList<RepositoryFile>> GetFileTreeAsync(
        string repoFullName, string commitSha, CancellationToken ct = default);

    /// <summary>
    /// Get the content of a specific file.
    /// </summary>
    Task<string> GetFileContentAsync(
        string repoFullName, string commitSha, string path, CancellationToken ct = default);
}

/// <summary>
/// Metadata about a file in the repository tree.
/// </summary>
public record RepositoryFile(string Path, string Type, long Size);
