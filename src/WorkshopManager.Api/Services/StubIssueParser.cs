using Octokit.Webhooks.Events;
using WorkshopManager.Models;

namespace WorkshopManager.Services;

/// <summary>
/// Stub implementation of IIssueParser for Phase 1.
/// Riri will swap in the real implementation in WI-04.
/// </summary>
public class StubIssueParser : IIssueParser
{
    public Task<bool> IsWorkshopUpgradeRequestAsync(IssuesEvent issuesEvent)
        => Task.FromResult(false);

    public Task<UpgradeIntent> ParseAsync(IssuesEvent issuesEvent)
        => Task.FromResult(UpgradeIntent.Empty);
}
