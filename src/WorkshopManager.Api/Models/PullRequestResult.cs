namespace WorkshopManager.Models;

/// <summary>
/// Result of creating a pull request from transformation results.
/// </summary>
public record PullRequestResult
{
    /// <summary>Whether the PR was created successfully.</summary>
    public required bool Success { get; init; }

    /// <summary>PR number, if created.</summary>
    public int? PullRequestNumber { get; init; }

    /// <summary>PR URL, if created.</summary>
    public string? PullRequestUrl { get; init; }

    /// <summary>Branch name used for the PR.</summary>
    public string? BranchName { get; init; }

    /// <summary>Number of commits created.</summary>
    public int CommitCount { get; init; }

    /// <summary>Error message if PR creation failed.</summary>
    public string? ErrorMessage { get; init; }
}
