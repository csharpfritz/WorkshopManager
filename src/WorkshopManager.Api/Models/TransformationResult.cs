namespace WorkshopManager.Models;

/// <summary>
/// Result of transforming a single content item through Copilot.
/// </summary>
public record TransformationResult
{
    /// <summary>Path relative to repo root.</summary>
    public required string Path { get; init; }

    /// <summary>Content type that determined the transformation strategy.</summary>
    public required ContentItemType ContentType { get; init; }

    /// <summary>Whether the transformation succeeded.</summary>
    public required bool Success { get; init; }

    /// <summary>Transformed content. Null on failure; original content preserved.</summary>
    public string? TransformedContent { get; init; }

    /// <summary>Original content before transformation.</summary>
    public required string OriginalContent { get; init; }

    /// <summary>Error message if transformation failed.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Copilot tokens consumed for this file.</summary>
    public int TokensUsed { get; init; }

    /// <summary>Whether the content actually changed (success + diff detected).</summary>
    public bool HasChanges => Success
        && TransformedContent is not null
        && TransformedContent != OriginalContent;
}
