namespace WorkshopManager.Models;

/// <summary>
/// Aggregated results from transforming all content items in a workshop upgrade.
/// </summary>
public record TransformationSummary
{
    /// <summary>All per-file results, including successes and failures.</summary>
    public required IReadOnlyList<TransformationResult> Results { get; init; }

    /// <summary>The upgrade intent that drove this transformation.</summary>
    public required UpgradeIntent Intent { get; init; }

    /// <summary>The workshop structure that was transformed.</summary>
    public required WorkshopStructure Structure { get; init; }

    // Computed properties
    public IReadOnlyList<TransformationResult> Succeeded =>
        Results.Where(r => r.HasChanges).ToList();

    public IReadOnlyList<TransformationResult> Failed =>
        Results.Where(r => !r.Success).ToList();

    public IReadOnlyList<TransformationResult> Unchanged =>
        Results.Where(r => r.Success && !r.HasChanges).ToList();

    public int TotalTokensUsed => Results.Sum(r => r.TokensUsed);

    public bool HasAnyChanges => Succeeded.Count > 0;
}
