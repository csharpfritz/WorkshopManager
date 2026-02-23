namespace WorkshopManager.Models;

/// <summary>
/// Top-level result from the upgrade orchestrator.
/// </summary>
public record UpgradeResult
{
    public required bool Success { get; init; }

    /// <summary>PR details if created.</summary>
    public PullRequestResult? PullRequest { get; init; }

    /// <summary>Transformation details.</summary>
    public TransformationSummary? TransformationSummary { get; init; }

    /// <summary>Error message for top-level failures (e.g., analysis failure).</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Which phase failed, if any.</summary>
    public UpgradePhase? FailedPhase { get; init; }
}

public enum UpgradePhase
{
    Analysis,
    Transformation,
    PullRequestGeneration
}
