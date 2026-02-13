using Octokit.Webhooks;
using Octokit.Webhooks.Events;
using Octokit.Webhooks.Events.Issues;
using Octokit.Webhooks.Events.PullRequest;
using WorkshopManager.Services;

namespace WorkshopManager.Webhooks;

public class WorkshopWebhookEventProcessor : WebhookEventProcessor
{
    private readonly IIssueParser _issueParser;
    private readonly ILogger<WorkshopWebhookEventProcessor> _logger;

    public WorkshopWebhookEventProcessor(
        IIssueParser issueParser,
        ILogger<WorkshopWebhookEventProcessor> logger)
    {
        _issueParser = issueParser;
        _logger = logger;
    }

    protected override async ValueTask ProcessIssuesWebhookAsync(
        WebhookHeaders headers,
        IssuesEvent issuesEvent,
        IssuesAction action,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Received issues webhook: action={Action}, issue=#{IssueNumber}",
            action,
            issuesEvent.Issue.Number);

        if (!await _issueParser.IsWorkshopUpgradeRequestAsync(issuesEvent))
        {
            _logger.LogDebug("Issue #{IssueNumber} is not a workshop upgrade request, skipping", issuesEvent.Issue.Number);
            return;
        }

        var intent = await _issueParser.ParseAsync(issuesEvent);

        _logger.LogInformation(
            "Parsed upgrade intent for issue #{IssueNumber}: {Technology} {SourceVersion} -> {TargetVersion}",
            issuesEvent.Issue.Number,
            intent.Technology,
            intent.SourceVersion,
            intent.TargetVersion);
    }

    protected override ValueTask ProcessPullRequestWebhookAsync(
        WebhookHeaders headers,
        PullRequestEvent pullRequestEvent,
        PullRequestAction action,
        CancellationToken cancellationToken = default)
    {
        // Phase 5: Dependabot trigger — log and return for now
        _logger.LogInformation(
            "Received pull_request webhook: action={Action}, PR=#{PrNumber}",
            action,
            pullRequestEvent.PullRequest.Number);

        return ValueTask.CompletedTask;
    }
}
