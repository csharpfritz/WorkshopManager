using WorkshopManager.Models;

namespace WorkshopManager.Services;

/// <summary>
/// Creates branches, commits transformed files in logical groups, and opens
/// pull requests via the GitHub API.
/// </summary>
public interface IPullRequestService
{
    /// <summary>
    /// Generate a PR from transformation results.
    /// Creates a branch, makes logical commits by content category, and opens a PR.
    /// </summary>
    /// <param name="summary">Transformation results to commit.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>PR creation result.</returns>
    Task<PullRequestResult> CreatePullRequestAsync(
        TransformationSummary summary,
        CancellationToken ct = default);
}
