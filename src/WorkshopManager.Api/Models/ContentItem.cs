namespace WorkshopManager.Models;

/// <summary>
/// A single content item (file) discovered in a workshop repository.
/// </summary>
public record ContentItem
{
    /// <summary>
    /// Path relative to the repository root (e.g., "src/Module01/Program.cs").
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Classification of this content item.
    /// </summary>
    public required ContentItemType Type { get; init; }

    /// <summary>
    /// Technology detected for this specific item, if different from the workshop-level technology.
    /// Null means "inherits from WorkshopStructure.Technology".
    /// </summary>
    public string? Technology { get; init; }

    /// <summary>
    /// Version references found in this file (e.g., TargetFramework value, engine version).
    /// Empty if no version information detected.
    /// </summary>
    public IReadOnlyList<VersionReference> VersionReferences { get; init; } = [];

    /// <summary>
    /// Dependencies declared in this file (e.g., NuGet packages, npm packages).
    /// Only populated for project/config files that declare dependencies.
    /// </summary>
    public IReadOnlyList<DependencyReference> Dependencies { get; init; } = [];

    /// <summary>
    /// Logical group this item belongs to (e.g., "module-01", "lab-03").
    /// Null if the item is at the workshop root level.
    /// </summary>
    public string? Group { get; init; }
}

/// <summary>
/// A technology version reference found within a file.
/// </summary>
public record VersionReference(
    string FrameworkOrRuntime,
    string Version,
    string Location);

/// <summary>
/// A dependency declared in a project or config file.
/// </summary>
public record DependencyReference(
    string Name,
    string Version,
    string? Source);
