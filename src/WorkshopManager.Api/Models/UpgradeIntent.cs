namespace WorkshopManager.Models;

public record UpgradeIntent(
    string SourceVersion,
    string TargetVersion,
    string Technology,
    UpgradeScope Scope,
    long IssueNumber,
    string IssueId,
    string RepoFullName,
    string RequestorLogin,
    string? ReleaseNotesUrl)
{
    public static UpgradeIntent Empty { get; } = new(
        SourceVersion: "unknown",
        TargetVersion: "unknown",
        Technology: "unknown",
        Scope: UpgradeScope.Full,
        IssueNumber: 0,
        IssueId: string.Empty,
        RepoFullName: string.Empty,
        RequestorLogin: string.Empty,
        ReleaseNotesUrl: null);
}

public enum UpgradeScope
{
    Full,
    CodeOnly,
    DocsOnly,
    Incremental
}
