using System.Text.RegularExpressions;
using WorkshopManager.Models;

namespace WorkshopManager.Services;

/// <summary>
/// Classifies repository files by extension and path patterns into ContentItemType categories.
/// </summary>
public partial class FileClassifier
{
    private static readonly HashSet<string> ExcludedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git",
        "node_modules",
        "bin",
        "obj",
        ".vs",
        "__pycache__"
    };

    // Module/section directory patterns
    [GeneratedRegex(@"^(?:modules?|steps?|chapters?|labs?|exercises?)/", RegexOptions.IgnoreCase)]
    private static partial Regex ModuleDirectoryPattern();

    [GeneratedRegex(@"^(?:chapter|module|lab|step|exercise)[\d-]", RegexOptions.IgnoreCase)]
    private static partial Regex NumberedSectionPattern();

    // Container directory patterns
    [GeneratedRegex(@"^(?:src|code|samples?)/", RegexOptions.IgnoreCase)]
    private static partial Regex CodeContainerPattern();

    [GeneratedRegex(@"^(?:docs|instructions?|content)/", RegexOptions.IgnoreCase)]
    private static partial Regex DocsContainerPattern();

    [GeneratedRegex(@"^(?:shared|common|assets)/", RegexOptions.IgnoreCase)]
    private static partial Regex SharedContainerPattern();

    /// <summary>
    /// Classify files from a repository tree into ContentItem objects.
    /// </summary>
    /// <param name="files">All files in the repository.</param>
    /// <param name="excludePaths">Additional paths to exclude from manifest.</param>
    /// <returns>List of classified ContentItem objects.</returns>
    public IReadOnlyList<ContentItem> ClassifyFiles(
        IReadOnlyList<RepositoryFile> files,
        IReadOnlyList<string>? excludePaths = null)
    {
        var items = new List<ContentItem>();
        var excludeSet = new HashSet<string>(excludePaths ?? [], StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            // Skip excluded paths
            if (ShouldExclude(file.Path, excludeSet))
                continue;

            var type = ClassifyFile(file.Path);
            if (type is null)
                continue;

            items.Add(new ContentItem
            {
                Path = file.Path,
                Type = type.Value
            });
        }

        // Sort: ProjectFile first, then CodeSample, Documentation, Configuration, Asset
        return items
            .OrderBy(i => i.Type switch
            {
                ContentItemType.ProjectFile => 0,
                ContentItemType.CodeSample => 1,
                ContentItemType.Documentation => 2,
                ContentItemType.Configuration => 3,
                ContentItemType.Asset => 4,
                _ => 5
            })
            .ThenBy(i => i.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Classify a single file by its path and extension.
    /// </summary>
    private ContentItemType? ClassifyFile(string path)
    {
        var fileName = Path.GetFileName(path);
        var extension = Path.GetExtension(path).ToLowerInvariant();
        var directoryPath = Path.GetDirectoryName(path)?.Replace('\\', '/') ?? string.Empty;

        // Workshop entry points
        if (fileName.Equals("README.md", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("WORKSHOP.md", StringComparison.OrdinalIgnoreCase))
            return ContentItemType.Documentation;

        // .NET project files
        if (extension is ".csproj" or ".fsproj" or ".sln")
            return ContentItemType.ProjectFile;

        // Node.js project
        if (fileName.Equals("package.json", StringComparison.OrdinalIgnoreCase))
            return ContentItemType.ProjectFile;

        // Python project
        if (fileName.Equals("requirements.txt", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("pyproject.toml", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("setup.py", StringComparison.OrdinalIgnoreCase))
            return ContentItemType.ProjectFile;

        // Go project
        if (fileName.Equals("go.mod", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("go.sum", StringComparison.OrdinalIgnoreCase))
            return ContentItemType.ProjectFile;

        // Java project
        if (fileName.Equals("pom.xml", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("build.gradle", StringComparison.OrdinalIgnoreCase))
            return ContentItemType.ProjectFile;

        // Container configuration
        if (fileName.Equals("Dockerfile", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("docker-compose.yml", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("docker-compose.yaml", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("devcontainer.json", StringComparison.OrdinalIgnoreCase) ||
            path.Contains(".devcontainer/", StringComparison.OrdinalIgnoreCase))
            return ContentItemType.Configuration;

        // Code samples
        if (extension is ".cs" or ".fs" or ".vb" or
            ".ts" or ".js" or ".tsx" or ".jsx" or
            ".py" or
            ".ps1" or ".sh" or ".bash" or
            ".go" or ".rs" or ".rb" or ".php" or
            ".java" or ".kt")
            return ContentItemType.CodeSample;

        // Documentation
        if (extension is ".md" or ".txt")
        {
            // Check if in a docs directory
            if (DocsContainerPattern().IsMatch(directoryPath))
                return ContentItemType.Documentation;

            // If not specifically in a code container, assume documentation
            if (!CodeContainerPattern().IsMatch(directoryPath))
                return ContentItemType.Documentation;
        }

        // Configuration files
        if (extension is ".json" or ".yml" or ".yaml" or ".toml" or ".xml" or ".config")
            return ContentItemType.Configuration;

        // Image assets
        if (extension is ".png" or ".jpg" or ".jpeg" or ".gif" or ".svg" or ".ico" or ".webp")
            return ContentItemType.Asset;

        // Unknown/not classified
        return null;
    }

    /// <summary>
    /// Determine if a file should be excluded from analysis.
    /// </summary>
    private bool ShouldExclude(string path, HashSet<string> additionalExcludes)
    {
        // Normalize path separators
        var normalizedPath = path.Replace('\\', '/');

        // Check excluded directories
        foreach (var excludedDir in ExcludedDirectories)
        {
            if (normalizedPath.StartsWith($"{excludedDir}/", StringComparison.OrdinalIgnoreCase) ||
                normalizedPath.Contains($"/{excludedDir}/", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // Check manifest excludes
        foreach (var excludePath in additionalExcludes)
        {
            var normalizedExclude = excludePath.Replace('\\', '/').TrimEnd('/');
            if (normalizedPath.StartsWith($"{normalizedExclude}/", StringComparison.OrdinalIgnoreCase) ||
                normalizedPath.Equals(normalizedExclude, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // Exclude the manifest itself
        if (normalizedPath.Equals(".workshop.yml", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    /// <summary>
    /// Infer logical groups from directory structure.
    /// </summary>
    public IReadOnlyList<ContentItem> InferGroups(IReadOnlyList<ContentItem> items)
    {
        var groupedItems = new List<ContentItem>();

        foreach (var item in items)
        {
            var normalizedPath = item.Path.Replace('\\', '/');
            var group = ExtractGroup(normalizedPath);

            groupedItems.Add(item with { Group = group });
        }

        return groupedItems;
    }

    /// <summary>
    /// Extract logical group name from path.
    /// </summary>
    private string? ExtractGroup(string normalizedPath)
    {
        var segments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        foreach (var segment in segments)
        {
            // Check for numbered section patterns
            if (NumberedSectionPattern().IsMatch(segment))
                return segment;

            // Check for module directories
            if (segment.Equals("modules", StringComparison.OrdinalIgnoreCase) ||
                segment.Equals("labs", StringComparison.OrdinalIgnoreCase) ||
                segment.Equals("exercises", StringComparison.OrdinalIgnoreCase) ||
                segment.Equals("chapters", StringComparison.OrdinalIgnoreCase) ||
                segment.Equals("steps", StringComparison.OrdinalIgnoreCase))
            {
                // Next segment is the group name
                var idx = Array.IndexOf(segments, segment);
                if (idx < segments.Length - 1)
                    return segments[idx + 1];
            }
        }

        return null;
    }

    /// <summary>
    /// Apply manifest-defined module groups to content items.
    /// </summary>
    public IReadOnlyList<ContentItem> ApplyManifestGroups(
        IReadOnlyList<ContentItem> items,
        IReadOnlyList<ManifestModule> modules)
    {
        var groupedItems = new List<ContentItem>();

        foreach (var item in items)
        {
            var normalizedPath = item.Path.Replace('\\', '/');
            string? group = null;

            // Find matching module
            foreach (var module in modules)
            {
                var modulePath = module.Path.Replace('\\', '/').TrimEnd('/');
                if (normalizedPath.StartsWith($"{modulePath}/", StringComparison.OrdinalIgnoreCase))
                {
                    group = Path.GetFileName(modulePath);
                    break;
                }
            }

            groupedItems.Add(item with { Group = group });
        }

        return groupedItems;
    }
}
