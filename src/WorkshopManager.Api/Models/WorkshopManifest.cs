namespace WorkshopManager.Models;

/// <summary>
/// Typed representation of a .workshop.yml manifest file.
/// </summary>
public record WorkshopManifest
{
    public string? Name { get; init; }
    public string? Description { get; init; }
    public ManifestTechnology? Technology { get; init; }
    public ManifestStructure? Structure { get; init; }
}

public record ManifestTechnology
{
    public required string Primary { get; init; }
    public string? Version { get; init; }
    public IReadOnlyList<string> Additional { get; init; } = [];
}

public record ManifestStructure
{
    public IReadOnlyList<ManifestModule> Modules { get; init; } = [];
    public IReadOnlyList<string> Shared { get; init; } = [];
    public IReadOnlyList<string> Exclude { get; init; } = [];
}

public record ManifestModule
{
    public required string Path { get; init; }
    public string? Code { get; init; }
    public string? Docs { get; init; }
    public string? Title { get; init; }
    public int? Order { get; init; }
}
