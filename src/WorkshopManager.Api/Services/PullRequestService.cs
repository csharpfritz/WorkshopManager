using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Octokit;
using WorkshopManager.Configuration;
using WorkshopManager.Models;

namespace WorkshopManager.Services;

/// <summary>
/// Creates branches, commits transformed files in logical groups, and opens
/// pull requests via the GitHub API using the Git Data API (remote-first, no local clone).
/// </summary>
public class PullRequestService : IPullRequestService
{
    private readonly GitHubAppOptions _options;
    private readonly ILogger<PullRequestService> _logger;
    private GitHubClient? _installationClient;
    private string? _cachedRepoFullName;

    /// <summary>
    /// Commit groups in the order they should be committed, per PRD §7.
    /// </summary>
    private static readonly (ContentItemType Type, string MessageTemplate)[] CommitGroups =
    [
        (ContentItemType.ProjectFile, "chore: update project files to {0}"),
        (ContentItemType.CodeSample, "refactor: update code samples for {0}"),
        (ContentItemType.Documentation, "docs: update instructions for {0}"),
        (ContentItemType.Configuration, "chore: update configuration files"),
    ];

    public PullRequestService(
        IOptions<GitHubAppOptions> options,
        ILogger<PullRequestService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<PullRequestResult> CreatePullRequestAsync(
        TransformationSummary summary,
        CancellationToken ct = default)
    {
        var intent = summary.Intent;
        var (owner, repo) = ParseRepoFullName(intent.RepoFullName);

        try
        {
            var client = await GetInstallationClientAsync(owner, repo);
            var branchName = BuildBranchName(intent);

            // 1. Get default branch HEAD SHA
            var repository = await client.Repository.Get(owner, repo);
            var defaultBranch = repository.DefaultBranch;
            var defaultBranchRef = await client.Git.Reference.Get(owner, repo, $"heads/{defaultBranch}");
            var baseSha = defaultBranchRef.Object.Sha;

            // 2. Create or reset branch
            await CreateOrResetBranchAsync(client, owner, repo, branchName, baseSha);

            // 3. Create commits by category
            var currentSha = baseSha;
            var commitCount = 0;

            foreach (var (contentType, messageTemplate) in CommitGroups)
            {
                var files = summary.Succeeded
                    .Where(r => r.ContentType == contentType)
                    .ToList();

                if (files.Count == 0)
                    continue;

                var commitMessage = contentType == ContentItemType.Configuration
                    ? messageTemplate
                    : string.Format(messageTemplate, intent.TargetVersion);

                currentSha = await CreateCommitAsync(
                    client, owner, repo, currentSha, files, commitMessage);
                commitCount++;

                // Advance branch ref to new commit
                await client.Git.Reference.Update(owner, repo, $"heads/{branchName}",
                    new ReferenceUpdate(currentSha));

                _logger.LogInformation(
                    "Created commit for {ContentType} ({FileCount} files) on {Branch}",
                    contentType, files.Count, branchName);
            }

            if (commitCount == 0)
            {
                return new PullRequestResult
                {
                    Success = false,
                    BranchName = branchName,
                    ErrorMessage = "No files with changes to commit."
                };
            }

            // 4. Create or update PR
            var prBody = BuildPrBody(summary);
            var prTitle = $"🔄 Workshop Upgrade: {intent.Technology} {intent.SourceVersion} → {intent.TargetVersion}";
            var pr = await CreateOrUpdatePullRequestAsync(
                client, owner, repo, branchName, defaultBranch, prTitle, prBody, intent);

            _logger.LogInformation(
                "PR #{PrNumber} created/updated for {Branch} with {CommitCount} commits",
                pr.Number, branchName, commitCount);

            return new PullRequestResult
            {
                Success = true,
                PullRequestNumber = pr.Number,
                PullRequestUrl = pr.HtmlUrl,
                BranchName = branchName,
                CommitCount = commitCount
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create PR for issue #{IssueNumber} in {Repo}",
                intent.IssueNumber, intent.RepoFullName);
            return new PullRequestResult
            {
                Success = false,
                ErrorMessage = $"PR generation failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Builds the branch name per design §6.1:
    /// workshop-upgrade/{issue-number}-{technology}-{target-version}
    /// </summary>
    internal static string BuildBranchName(UpgradeIntent intent)
    {
        var techSlug = Regex.Replace(intent.Technology.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
        var versionSlug = Regex.Replace(intent.TargetVersion, @"[^a-zA-Z0-9.]+", "-").Trim('-');
        return $"workshop-upgrade/{intent.IssueNumber}-{techSlug}-{versionSlug}";
    }

    /// <summary>
    /// Creates the branch if it doesn't exist, or resets it to baseSha if it does (idempotency per design §6.4).
    /// </summary>
    private async Task CreateOrResetBranchAsync(
        GitHubClient client, string owner, string repo, string branchName, string baseSha)
    {
        try
        {
            // Try to get existing branch
            await client.Git.Reference.Get(owner, repo, $"heads/{branchName}");

            // Branch exists — delete and recreate from current HEAD (per design D5)
            _logger.LogInformation("Branch {Branch} already exists, resetting to {Sha}", branchName, baseSha);
            await client.Git.Reference.Delete(owner, repo, $"heads/{branchName}");
        }
        catch (NotFoundException)
        {
            // Branch doesn't exist — expected for first run
        }

        await client.Git.Reference.Create(owner, repo,
            new NewReference($"refs/heads/{branchName}", baseSha));

        _logger.LogDebug("Branch {Branch} created at {Sha}", branchName, baseSha);
    }

    /// <summary>
    /// Creates a single commit via the Git Data API (blobs → tree → commit).
    /// </summary>
    private static async Task<string> CreateCommitAsync(
        GitHubClient client, string owner, string repo,
        string parentSha, IReadOnlyList<TransformationResult> files, string message)
    {
        // Build tree items from transformed files
        var newTree = new NewTree { BaseTree = parentSha };
        foreach (var file in files)
        {
            newTree.Tree.Add(new NewTreeItem
            {
                Path = file.Path,
                Mode = "100644", // regular file
                Type = TreeType.Blob,
                Content = file.TransformedContent
            });
        }

        var tree = await client.Git.Tree.Create(owner, repo, newTree);

        var newCommit = new NewCommit(message, tree.Sha, parentSha);
        var commit = await client.Git.Commit.Create(owner, repo, newCommit);

        return commit.Sha;
    }

    /// <summary>
    /// Creates a new PR or updates an existing one for the branch (idempotency per design §6.4).
    /// </summary>
    private async Task<PullRequest> CreateOrUpdatePullRequestAsync(
        GitHubClient client, string owner, string repo,
        string branchName, string baseBranch, string title, string body,
        UpgradeIntent intent)
    {
        // Check for existing PR on this branch
        var existingPrs = await client.PullRequest.GetAllForRepository(owner, repo,
            new PullRequestRequest
            {
                Head = $"{owner}:{branchName}",
                State = ItemStateFilter.Open
            });

        PullRequest pr;

        if (existingPrs.Count > 0)
        {
            // Update existing PR
            pr = existingPrs[0];
            await client.PullRequest.Update(owner, repo, pr.Number,
                new PullRequestUpdate
                {
                    Title = title,
                    Body = body
                });

            _logger.LogInformation("Updated existing PR #{PrNumber}", pr.Number);

            // Re-fetch to get updated data
            pr = await client.PullRequest.Get(owner, repo, pr.Number);
        }
        else
        {
            // Create new PR
            var newPr = new NewPullRequest(title, branchName, baseBranch)
            {
                Body = body
            };

            pr = await client.PullRequest.Create(owner, repo, newPr);
            _logger.LogInformation("Created new PR #{PrNumber}", pr.Number);
        }

        // Apply labels
        await ApplyLabelsAsync(client, owner, repo, pr.Number, intent);

        // Link issue via body (Closes #N is in the body already)
        // Assign requestor
        if (!string.IsNullOrEmpty(intent.RequestorLogin))
        {
            try
            {
                await client.Issue.Assignee.AddAssignees(
                    owner, repo, pr.Number,
                    new AssigneesUpdate(new List<string> { intent.RequestorLogin }));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Could not assign {User} to PR #{PrNumber}. They may not have repo access.",
                    intent.RequestorLogin, pr.Number);
            }
        }

        return pr;
    }

    /// <summary>
    /// Applies standard labels to the PR, creating them if they don't exist.
    /// </summary>
    private async Task ApplyLabelsAsync(
        GitHubClient client, string owner, string repo, int prNumber, UpgradeIntent intent)
    {
        var labels = new List<string> { "workshop-upgrade", "automated" };

        var techLabel = intent.Technology.ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(techLabel))
            labels.Add(techLabel);

        // Ensure labels exist
        foreach (var label in labels)
        {
            try
            {
                await client.Issue.Labels.Get(owner, repo, label);
            }
            catch (NotFoundException)
            {
                try
                {
                    await client.Issue.Labels.Create(owner, repo,
                        new NewLabel(label, "0075ca"));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not create label '{Label}'", label);
                }
            }
        }

        await client.Issue.Labels.AddToIssue(owner, repo, prNumber, labels.ToArray());
    }

    /// <summary>
    /// Builds the structured PR description per design §6.3.
    /// </summary>
    internal static string BuildPrBody(TransformationSummary summary)
    {
        var intent = summary.Intent;
        var sb = new StringBuilder();

        // Header
        sb.AppendLine($"## 🔄 Workshop Upgrade: {intent.Technology} {intent.SourceVersion} → {intent.TargetVersion}");
        sb.AppendLine();
        sb.AppendLine($"This PR upgrades the workshop content from {intent.Technology} {intent.SourceVersion} to {intent.TargetVersion}.");
        sb.AppendLine();
        sb.AppendLine($"Requested by @{intent.RequestorLogin} in #{intent.IssueNumber}.");
        sb.AppendLine();

        // Summary table
        sb.AppendLine("### Summary");
        sb.AppendLine();
        sb.AppendLine("| Category | Files Changed | Files Failed | Files Unchanged |");
        sb.AppendLine("|----------|:------------:|:------------:|:---------------:|");

        var categories = new[]
        {
            (ContentItemType.ProjectFile, "Project Files"),
            (ContentItemType.CodeSample, "Code Samples"),
            (ContentItemType.Documentation, "Documentation"),
            (ContentItemType.Configuration, "Configuration"),
        };

        var totalChanged = 0;
        var totalFailed = 0;
        var totalUnchanged = 0;

        foreach (var (type, label) in categories)
        {
            var changed = summary.Succeeded.Count(r => r.ContentType == type);
            var failed = summary.Failed.Count(r => r.ContentType == type);
            var unchanged = summary.Unchanged.Count(r => r.ContentType == type);

            totalChanged += changed;
            totalFailed += failed;
            totalUnchanged += unchanged;

            sb.AppendLine($"| {label} | {changed} | {failed} | {unchanged} |");
        }

        sb.AppendLine($"| **Total** | **{totalChanged}** | **{totalFailed}** | **{totalUnchanged}** |");
        sb.AppendLine();
        sb.AppendLine($"Copilot tokens used: {summary.TotalTokensUsed}");
        sb.AppendLine();

        // Changes by category
        sb.AppendLine("### Changes by Category");
        sb.AppendLine();

        foreach (var (type, label) in categories)
        {
            var items = summary.Results.Where(r => r.ContentType == type).ToList();
            if (items.Count == 0)
                continue;

            sb.AppendLine($"#### {label}");
            sb.AppendLine("| File | Status |");
            sb.AppendLine("|------|--------|");

            foreach (var item in items)
            {
                var status = item switch
                {
                    { HasChanges: true } => "✅ Updated",
                    { Success: true, HasChanges: false } => "⏭️ No changes needed",
                    _ => $"❌ Failed"
                };
                sb.AppendLine($"| `{item.Path}` | {status} |");
            }
            sb.AppendLine();
        }

        // Failures section
        if (summary.Failed.Count > 0)
        {
            sb.AppendLine("### ⚠️ Failures");
            sb.AppendLine();
            sb.AppendLine("The following files could not be transformed and were **not included** in this PR:");
            sb.AppendLine();
            sb.AppendLine("| File | Error |");
            sb.AppendLine("|------|-------|");

            foreach (var fail in summary.Failed)
            {
                var error = fail.ErrorMessage ?? "Unknown error";
                sb.AppendLine($"| `{fail.Path}` | {error} |");
            }
            sb.AppendLine();
        }

        // Review markers
        var reviewFiles = summary.Succeeded
            .Where(r => r.TransformedContent?.Contains("REVIEW:", StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        if (reviewFiles.Count > 0)
        {
            sb.AppendLine("### 👀 Review Markers");
            sb.AppendLine();
            sb.AppendLine("Files containing `REVIEW:` markers that need human attention:");
            sb.AppendLine();

            foreach (var file in reviewFiles)
            {
                sb.AppendLine($"- `{file.Path}`");
            }
            sb.AppendLine();
        }

        // Notes
        sb.AppendLine("### Notes");
        sb.AppendLine($"- This PR was generated by WorkshopManager in response to #{intent.IssueNumber}");
        sb.AppendLine("- Review carefully before merging");
        if (summary.Unchanged.Count > 0)
        {
            sb.AppendLine($"- {summary.Unchanged.Count} file(s) required no changes (already at target version)");
        }
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine($"Closes #{intent.IssueNumber}");

        return sb.ToString();
    }

    /// <summary>
    /// Obtains an authenticated GitHubClient for the App installation on the given repository.
    /// Follows the same pattern as GitHubContentProvider.
    /// </summary>
    private async Task<GitHubClient> GetInstallationClientAsync(string owner, string repo)
    {
        var repoFullName = $"{owner}/{repo}";

        if (_installationClient is not null && _cachedRepoFullName == repoFullName)
            return _installationClient;

        var jwt = GenerateJwt();
        var appClient = new GitHubClient(new ProductHeaderValue(_options.AppName))
        {
            Credentials = new Credentials(jwt, AuthenticationType.Bearer)
        };

        try
        {
            var installation = await appClient.GitHubApps.GetRepositoryInstallationForCurrent(owner, repo);
            var tokenResponse = await appClient.GitHubApps.CreateInstallationToken(installation.Id);

            _installationClient = new GitHubClient(new ProductHeaderValue(_options.AppName))
            {
                Credentials = new Credentials(tokenResponse.Token)
            };
            _cachedRepoFullName = repoFullName;

            _logger.LogDebug(
                "Obtained installation token for {Repo} (installation {InstallationId}).",
                repoFullName, installation.Id);

            return _installationClient;
        }
        catch (NotFoundException)
        {
            _logger.LogError("GitHub App is not installed on repository {Repo}.", repoFullName);
            throw new InvalidOperationException(
                $"GitHub App is not installed on repository '{repoFullName}'.");
        }
        catch (AuthorizationException ex)
        {
            _logger.LogError(ex,
                "Authentication failed for GitHub App {AppId}. Verify AppId and PrivateKey.",
                _options.AppId);
            throw new InvalidOperationException(
                "Authentication failed for GitHub App. Verify AppId and PrivateKey configuration.", ex);
        }
    }

    /// <summary>
    /// Generates a short-lived JWT for GitHub App authentication using RS256.
    /// Follows the same pattern as GitHubContentProvider.
    /// </summary>
    private string GenerateJwt()
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(_options.PrivateKey);

        var now = DateTimeOffset.UtcNow;

        var headerBytes = JsonSerializer.SerializeToUtf8Bytes(
            new { alg = "RS256", typ = "JWT" });
        var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            iat = now.AddSeconds(-60).ToUnixTimeSeconds(),
            exp = now.AddMinutes(10).ToUnixTimeSeconds(),
            iss = _options.AppId
        });

        var header = Base64UrlEncode(headerBytes);
        var payload = Base64UrlEncode(payloadBytes);

        var dataToSign = Encoding.UTF8.GetBytes($"{header}.{payload}");
        var signature = Base64UrlEncode(
            rsa.SignData(dataToSign, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));

        return $"{header}.{payload}.{signature}";
    }

    private static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static (string Owner, string Repo) ParseRepoFullName(string repoFullName)
    {
        var parts = repoFullName.Split('/', 2);
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
            throw new ArgumentException(
                $"Invalid repository full name: '{repoFullName}'. Expected 'owner/repo' format.",
                nameof(repoFullName));
        return (parts[0], parts[1]);
    }
}
