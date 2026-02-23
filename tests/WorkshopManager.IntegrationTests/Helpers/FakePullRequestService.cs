using WorkshopManager.Models;
using WorkshopManager.Services;

namespace WorkshopManager.IntegrationTests.Helpers;

/// <summary>
/// Fake PR service that records calls and returns configurable results.
/// Avoids hitting the real GitHub API during integration tests.
/// </summary>
public class FakePullRequestService : IPullRequestService
{
    public TransformationSummary? LastSummary { get; private set; }
    public int CallCount { get; private set; }

    public PullRequestResult ResultToReturn { get; set; } = new()
    {
        Success = true,
        PullRequestNumber = 42,
        PullRequestUrl = "https://github.com/test-owner/test-repo/pull/42",
        BranchName = "workshop-upgrade/1-dotnet-10.0",
        CommitCount = 3
    };

    public Task<PullRequestResult> CreatePullRequestAsync(
        TransformationSummary summary, CancellationToken ct = default)
    {
        LastSummary = summary;
        CallCount++;
        return Task.FromResult(ResultToReturn);
    }
}
