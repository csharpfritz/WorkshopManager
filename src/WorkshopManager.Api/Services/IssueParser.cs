using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Octokit.Webhooks.Events;
using WorkshopManager.Models;

namespace WorkshopManager.Services;

public partial class IssueParser : IIssueParser
{
    private const string WorkshopUpgradeLabel = "workshop-upgrade";

    private static readonly string[] TechnologyKeywords =
    [
        ".NET", "C#", "ASP.NET", "Blazor", "MAUI",
        "Python", "Django", "Flask", "FastAPI",
        "Node.js", "TypeScript", "JavaScript", "React", "Angular", "Vue",
        "Java", "Spring", "Kotlin",
        "Go", "Rust", "Ruby", "PHP"
    ];

    private readonly ILogger<IssueParser> _logger;
    private readonly string _appName;

    public IssueParser(ILogger<IssueParser> logger, IConfiguration configuration)
    {
        _logger = logger;
        _appName = configuration.GetValue<string>("GitHubApp:AppName") ?? "workshop-manager[bot]";
    }

    public Task<bool> IsWorkshopUpgradeRequestAsync(IssuesEvent issuesEvent)
    {
        var issue = issuesEvent.Issue;

        bool hasLabel = issue.Labels?.Any(
            l => l.Name.Equals(WorkshopUpgradeLabel, StringComparison.OrdinalIgnoreCase)) == true;

        bool isAssignedToBot = issue.Assignees?.Any(
            a => a.Login.Equals(_appName, StringComparison.OrdinalIgnoreCase)) == true;

        _logger.LogDebug(
            "Issue #{IssueNumber} — label match: {HasLabel}, bot assigned: {IsAssigned}",
            issue.Number, hasLabel, isAssignedToBot);

        return Task.FromResult(hasLabel || isAssignedToBot);
    }

    public Task<UpgradeIntent> ParseAsync(IssuesEvent issuesEvent)
    {
        var issue = issuesEvent.Issue;
        var title = issue.Title ?? string.Empty;
        var body = issue.Body ?? string.Empty;
        var repoFullName = issuesEvent.Repository?.FullName ?? "unknown/unknown";
        var requestorLogin = issue.User?.Login ?? "unknown";
        var issueId = issue.NodeId ?? string.Empty;

        var targetVersion = ExtractTargetVersion(title, body);
        var sourceVersion = ExtractSourceVersion(title, body);
        var technology = DetectTechnology(title, body);
        var scope = ParseScope(body);
        var releaseNotesUrl = ExtractReleaseNotesUrl(body);

        var intent = new UpgradeIntent(
            SourceVersion: sourceVersion,
            TargetVersion: targetVersion,
            Technology: technology,
            Scope: scope,
            IssueNumber: issue.Number,
            IssueId: issueId,
            RepoFullName: repoFullName,
            RequestorLogin: requestorLogin,
            ReleaseNotesUrl: releaseNotesUrl);

        _logger.LogInformation(
            "Parsed issue #{IssueNumber}: {Technology} {Source} → {Target}, scope={Scope}",
            issue.Number, intent.Technology, intent.SourceVersion, intent.TargetVersion, intent.Scope);

        return Task.FromResult(intent);
    }

    // "Upgrade to X", "Update to X", "Migrate to X"
    [GeneratedRegex(@"(?:upgrade|update|migrate)\s+to\s+v?(\S+)", RegexOptions.IgnoreCase)]
    private static partial Regex TitleTargetPattern();

    // "Update from X to Y", "Upgrade from X to Y"
    [GeneratedRegex(@"(?:upgrade|update|migrate)\s+from\s+v?(\S+)\s+to\s+v?(\S+)", RegexOptions.IgnoreCase)]
    private static partial Regex TitleFromToPattern();

    // Structured body field: **To:** value
    [GeneratedRegex(@"\*\*To:\*\*\s*v?(\S+)", RegexOptions.IgnoreCase)]
    private static partial Regex BodyToFieldPattern();

    // Structured body field: **From:** value
    [GeneratedRegex(@"\*\*From:\*\*\s*v?(\S+)", RegexOptions.IgnoreCase)]
    private static partial Regex BodyFromFieldPattern();

    // Structured body field: **Scope:** value
    [GeneratedRegex(@"\*\*Scope:\*\*\s*(\S+)", RegexOptions.IgnoreCase)]
    private static partial Regex BodyScopeFieldPattern();

    // Structured body field: **Release Notes:** URL
    [GeneratedRegex(@"\*\*Release\s+Notes:\*\*\s*(https?://\S+)", RegexOptions.IgnoreCase)]
    private static partial Regex BodyReleaseNotesFieldPattern();

    // Fallback URL detection for release notes
    [GeneratedRegex(@"(https?://\S*(?:release|changelog|what(?:'|')?s[ -]?new)\S*)", RegexOptions.IgnoreCase)]
    private static partial Regex FallbackReleaseNotesUrlPattern();

    private static string ExtractTargetVersion(string title, string body)
    {
        // Prefer structured body field
        var bodyMatch = BodyToFieldPattern().Match(body);
        if (bodyMatch.Success)
            return bodyMatch.Groups[1].Value;

        // Try "from X to Y" in title
        var fromToMatch = TitleFromToPattern().Match(title);
        if (fromToMatch.Success)
            return fromToMatch.Groups[2].Value;

        // Try "upgrade to X" in title
        var targetMatch = TitleTargetPattern().Match(title);
        if (targetMatch.Success)
            return targetMatch.Groups[1].Value;

        // Try same patterns in body text
        fromToMatch = TitleFromToPattern().Match(body);
        if (fromToMatch.Success)
            return fromToMatch.Groups[2].Value;

        targetMatch = TitleTargetPattern().Match(body);
        if (targetMatch.Success)
            return targetMatch.Groups[1].Value;

        return "latest";
    }

    private static string ExtractSourceVersion(string title, string body)
    {
        // Prefer structured body field
        var bodyMatch = BodyFromFieldPattern().Match(body);
        if (bodyMatch.Success)
            return bodyMatch.Groups[1].Value;

        // Try "from X to Y" in title
        var fromToMatch = TitleFromToPattern().Match(title);
        if (fromToMatch.Success)
            return fromToMatch.Groups[1].Value;

        // Try same pattern in body
        fromToMatch = TitleFromToPattern().Match(body);
        if (fromToMatch.Success)
            return fromToMatch.Groups[1].Value;

        return "current";
    }

    private static string DetectTechnology(string title, string body)
    {
        var combined = $"{title} {body}";

        foreach (var keyword in TechnologyKeywords)
        {
            if (combined.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                return keyword;
        }

        return "unknown";
    }

    private static UpgradeScope ParseScope(string body)
    {
        var match = BodyScopeFieldPattern().Match(body);
        if (!match.Success)
            return UpgradeScope.Full;

        return match.Groups[1].Value.ToLowerInvariant() switch
        {
            "code-only" or "codeonly" or "code" => UpgradeScope.CodeOnly,
            "docs-only" or "docsonly" or "docs" => UpgradeScope.DocsOnly,
            "incremental" => UpgradeScope.Incremental,
            _ => UpgradeScope.Full
        };
    }

    private static string? ExtractReleaseNotesUrl(string body)
    {
        // Prefer structured field
        var fieldMatch = BodyReleaseNotesFieldPattern().Match(body);
        if (fieldMatch.Success)
            return fieldMatch.Groups[1].Value;

        // Fallback: detect release-notes-like URLs
        var urlMatch = FallbackReleaseNotesUrlPattern().Match(body);
        return urlMatch.Success ? urlMatch.Groups[1].Value : null;
    }
}
