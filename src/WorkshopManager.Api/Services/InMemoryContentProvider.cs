namespace WorkshopManager.Services;

/// <summary>
/// In-memory implementation of IRepositoryContentProvider for testing.
/// </summary>
public class InMemoryContentProvider : IRepositoryContentProvider
{
    private readonly Dictionary<string, string> _files = new();

    /// <summary>
    /// Add a file to the in-memory repository.
    /// </summary>
    public void AddFile(string path, string content)
    {
        _files[path] = content;
    }

    /// <summary>
    /// Add multiple files at once.
    /// </summary>
    public void AddFiles(Dictionary<string, string> files)
    {
        foreach (var (path, content) in files)
        {
            _files[path] = content;
        }
    }

    /// <summary>
    /// Clear all files from the in-memory repository.
    /// </summary>
    public void Clear()
    {
        _files.Clear();
    }

    public Task<IReadOnlyList<RepositoryFile>> GetFileTreeAsync(
        string repoFullName,
        string commitSha,
        CancellationToken ct = default)
    {
        var files = _files.Keys
            .Select(p => new RepositoryFile(p, "blob", _files[p].Length))
            .ToList();
        return Task.FromResult<IReadOnlyList<RepositoryFile>>(files);
    }

    public Task<string> GetFileContentAsync(
        string repoFullName,
        string commitSha,
        string path,
        CancellationToken ct = default)
    {
        if (_files.TryGetValue(path, out var content))
            return Task.FromResult(content);

        throw new FileNotFoundException($"Test file not found: {path}");
    }
}
