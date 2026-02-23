namespace WorkshopManager.Models;

/// <summary>
/// Represents the discovered structure of a workshop repository.
/// Produced by IWorkshopAnalyzer, consumed by transformation services.
/// </summary>
public record WorkshopStructure
{
    /// <summary>
    /// Root path of the workshop within the repository (usually "/").
    /// </summary>
    public required string RootPath { get; init; }

    /// <summary>
    /// Primary technology detected (e.g., "dotnet", "node", "python").
    /// </summary>
    public required string Technology { get; init; }

    /// <summary>
    /// Detected version of the primary technology (e.g., "8.0", "20").
    /// Null if version could not be determined.
    /// </summary>
    public string? TechnologyVersion { get; init; }

    /// <summary>
    /// The parsed manifest, if .workshop.yml was found. Null for pure convention-based detection.
    /// </summary>
    public WorkshopManifest? Manifest { get; init; }

    /// <summary>
    /// All discovered content items in the workshop.
    /// </summary>
    public required IReadOnlyList<ContentItem> Items { get; init; }

    /// <summary>
    /// Detection strategy that produced this structure.
    /// </summary>
    public required DetectionStrategy Strategy { get; init; }

    /// <summary>
    /// Warnings or notes from the detection process (e.g., "No project files found").
    /// </summary>
    public IReadOnlyList<string> Diagnostics { get; init; } = [];
}

/// <summary>
/// Strategy used to detect workshop structure.
/// </summary>
public enum DetectionStrategy
{
    /// <summary>Structure derived entirely from .workshop.yml manifest.</summary>
    Manifest,

    /// <summary>Structure inferred from directory/file conventions.</summary>
    Convention,

    /// <summary>Manifest provided partial info; conventions filled gaps.</summary>
    Hybrid
}
