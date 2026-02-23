using WorkshopManager.Models;

namespace WorkshopManager.Services;

/// <summary>
/// Orchestrates the complete workshop upgrade pipeline:
/// issue parsing → content analysis → transformation → PR generation.
/// </summary>
public interface IUpgradeOrchestrator
{
    /// <summary>
    /// Execute the full upgrade workflow for a parsed upgrade intent.
    /// </summary>
    /// <param name="intent">Parsed upgrade intent from issue.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The PR result, or failure information.</returns>
    Task<UpgradeResult> ExecuteAsync(
        UpgradeIntent intent,
        CancellationToken ct = default);
}
