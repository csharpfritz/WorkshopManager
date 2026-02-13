using Octokit.Webhooks.Events;
using WorkshopManager.Models;

namespace WorkshopManager.Services;

public interface IIssueParser
{
    Task<bool> IsWorkshopUpgradeRequestAsync(IssuesEvent issuesEvent);
    Task<UpgradeIntent> ParseAsync(IssuesEvent issuesEvent);
}
