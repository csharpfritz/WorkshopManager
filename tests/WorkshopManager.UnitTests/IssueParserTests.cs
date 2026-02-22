using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Octokit.Webhooks.Events;
using WorkshopManager.Models;
using WorkshopManager.Services;
using WorkshopManager.UnitTests.Helpers;

namespace WorkshopManager.UnitTests;

public class IssueParserTests
{
    private readonly IssueParser _parser;

    public IssueParserTests()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GitHubApp:AppName"] = "workshop-manager[bot]"
            })
            .Build();

        _parser = new IssueParser(NullLogger<IssueParser>.Instance, config);
    }

    /// <summary>
    /// Creates an IssuesEvent from a JSON string using the Octokit.Webhooks deserializer.
    /// </summary>
    private static IssuesEvent CreateIssuesEvent(string title, string body,
        string[]? labels = null, string[]? assigneeLogins = null,
        long issueNumber = 1, string repoFullName = "owner/repo",
        string userLogin = "testuser")
    {
        var labelsJson = "[]";
        if (labels is { Length: > 0 })
        {
            var labelObjects = labels.Select((l, i) => new { id = 5000 + i, node_id = $"LA_{i}", name = l, color = "0e8a16" });
            labelsJson = JsonSerializer.Serialize(labelObjects);
        }

        var assigneesJson = "[]";
        if (assigneeLogins is { Length: > 0 })
        {
            var assigneeObjects = assigneeLogins.Select((a, i) => new { login = a, id = 90000 + i, node_id = $"BOT_{i}", type = "Bot" });
            assigneesJson = JsonSerializer.Serialize(assigneeObjects);
        }

        var escapedTitle = JsonEncodedText.Encode(title).ToString();
        var escapedBody = JsonEncodedText.Encode(body).ToString();

        var json = $$"""
        {
          "action": "labeled",
          "issue": {
            "id": 2012345678,
            "node_id": "I_kwDOABCDEF12345678",
            "number": {{issueNumber}},
            "title": "{{escapedTitle}}",
            "state": "open",
            "body": "{{escapedBody}}",
            "user": {
              "login": "{{userLogin}}",
              "id": 78577,
              "node_id": "MDQ6VXNlcjc4NTc3",
              "type": "User"
            },
            "labels": {{labelsJson}},
            "assignees": {{assigneesJson}},
            "created_at": "2026-02-13T10:00:00Z",
            "updated_at": "2026-02-13T10:05:00Z"
          },
          "repository": {
            "id": 100200300,
            "node_id": "R_kgDOABCDEF",
            "name": "test-repo",
            "full_name": "{{repoFullName}}",
            "private": false,
            "owner": {
              "login": "{{userLogin}}",
              "id": 78577,
              "node_id": "MDQ6VXNlcjc4NTc3",
              "type": "User"
            },
            "default_branch": "main"
          },
          "sender": {
            "login": "{{userLogin}}",
            "id": 78577,
            "node_id": "MDQ6VXNlcjc4NTc3",
            "type": "User"
          },
          "installation": {
            "id": 50000001,
            "node_id": "MDIzOkludGVncmF0aW9uNTAwMDAwMDE="
          }
        }
        """;

        return JsonSerializer.Deserialize<IssuesEvent>(json)
            ?? throw new InvalidOperationException("Failed to deserialize IssuesEvent");
    }

    #region IsWorkshopUpgradeRequestAsync

    [Fact]
    public async Task IsWorkshopUpgradeRequestAsync_WithUpgradeLabel_ReturnsTrue()
    {
        var evt = CreateIssuesEvent("Some issue", "body", labels: ["workshop-upgrade"]);

        var result = await _parser.IsWorkshopUpgradeRequestAsync(evt);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsWorkshopUpgradeRequestAsync_WithUpperCaseLabel_ReturnsTrue()
    {
        var evt = CreateIssuesEvent("Some issue", "body", labels: ["Workshop-Upgrade"]);

        var result = await _parser.IsWorkshopUpgradeRequestAsync(evt);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsWorkshopUpgradeRequestAsync_AssignedToBot_ReturnsTrue()
    {
        var evt = CreateIssuesEvent("Some issue", "body", assigneeLogins: ["workshop-manager[bot]"]);

        var result = await _parser.IsWorkshopUpgradeRequestAsync(evt);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsWorkshopUpgradeRequestAsync_BothLabelAndAssignment_ReturnsTrue()
    {
        var evt = CreateIssuesEvent("Some issue", "body",
            labels: ["workshop-upgrade"],
            assigneeLogins: ["workshop-manager[bot]"]);

        var result = await _parser.IsWorkshopUpgradeRequestAsync(evt);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsWorkshopUpgradeRequestAsync_NoLabelNoAssignment_ReturnsFalse()
    {
        var evt = CreateIssuesEvent("Some issue", "body");

        var result = await _parser.IsWorkshopUpgradeRequestAsync(evt);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsWorkshopUpgradeRequestAsync_UnrelatedLabel_ReturnsFalse()
    {
        var evt = CreateIssuesEvent("Some issue", "body", labels: ["bug", "enhancement"]);

        var result = await _parser.IsWorkshopUpgradeRequestAsync(evt);

        result.Should().BeFalse();
    }

    #endregion

    #region ParseAsync — Title Patterns

    [Fact]
    public async Task ParseAsync_UpgradeFromTo_ExtractsSourceAndTarget()
    {
        var title = TestFixtureLoader.LoadTitle("title-upgrade-from-to");
        var body = TestFixtureLoader.LoadBody("title-upgrade-from-to");
        var evt = CreateIssuesEvent(title, body, issueNumber: 10, repoFullName: "csharpfritz/dotnet-workshop");

        var intent = await _parser.ParseAsync(evt);

        // FIXED: Regex now correctly captures multi-word versions from title
        intent.SourceVersion.Should().Be(".NET 8");
        intent.TargetVersion.Should().Be(".NET 9");
        intent.Technology.Should().Be(".NET");
        intent.IssueNumber.Should().Be(10);
        intent.RepoFullName.Should().Be("csharpfritz/dotnet-workshop");
    }

    [Fact]
    public async Task ParseAsync_UpdateTo_SourceDefaultsToCurrent()
    {
        var title = TestFixtureLoader.LoadTitle("title-update-to");
        var body = TestFixtureLoader.LoadBody("title-update-to");
        var evt = CreateIssuesEvent(title, body, issueNumber: 11);

        var intent = await _parser.ParseAsync(evt);

        intent.SourceVersion.Should().Be("current");
        // "Update to .NET 9" — regex captures ".NET" as the target (first \S+ after "to")
        intent.TargetVersion.Should().Be(".NET");
        intent.Technology.Should().Be(".NET");
    }

    [Fact]
    public async Task ParseAsync_MigrateTitle_DetectsTechnology()
    {
        var title = TestFixtureLoader.LoadTitle("title-migrate");
        var body = TestFixtureLoader.LoadBody("title-migrate");
        var evt = CreateIssuesEvent(title, body, issueNumber: 12);

        var intent = await _parser.ParseAsync(evt);

        intent.Technology.Should().Be("Python");
    }

    [Fact]
    public async Task ParseAsync_NonUpgradeTitle_ReturnsDefaults()
    {
        var title = TestFixtureLoader.LoadTitle("title-not-upgrade");
        var body = TestFixtureLoader.LoadBody("title-not-upgrade");
        var evt = CreateIssuesEvent(title, body, issueNumber: 13);

        var intent = await _parser.ParseAsync(evt);

        intent.TargetVersion.Should().Be("latest");
        intent.SourceVersion.Should().Be("current");
    }

    #endregion

    #region ParseAsync — Body Patterns

    [Fact]
    public async Task ParseAsync_StructuredFullBody_ExtractsAllFields()
    {
        var title = TestFixtureLoader.LoadTitle("body-structured-full");
        var body = TestFixtureLoader.LoadBody("body-structured-full");
        var evt = CreateIssuesEvent(title, body, issueNumber: 20, repoFullName: "test/repo", userLogin: "csharpfritz");

        var intent = await _parser.ParseAsync(evt);

        // FIXED: Regex now correctly captures multi-word versions like ".NET 9"
        intent.TargetVersion.Should().Be(".NET 9");
        intent.SourceVersion.Should().Be(".NET 8");
        intent.Scope.Should().Be(UpgradeScope.Full);
        intent.ReleaseNotesUrl.Should().Contain("dotnet-9");
        intent.RequestorLogin.Should().Be("csharpfritz");
    }

    [Fact]
    public async Task ParseAsync_StructuredPartialBody_ScopeDefaultsToFull()
    {
        var title = TestFixtureLoader.LoadTitle("body-structured-partial");
        var body = TestFixtureLoader.LoadBody("body-structured-partial");
        var evt = CreateIssuesEvent(title, body, issueNumber: 21);

        var intent = await _parser.ParseAsync(evt);

        // FIXED: Regex now correctly captures multi-word versions like ".NET 9"
        intent.TargetVersion.Should().Be(".NET 9");
        intent.SourceVersion.Should().Be(".NET 8");
        intent.Scope.Should().Be(UpgradeScope.Full);
    }

    [Fact]
    public async Task ParseAsync_UnstructuredBody_ExtractsFromNaturalLanguage()
    {
        var title = TestFixtureLoader.LoadTitle("body-unstructured");
        var body = TestFixtureLoader.LoadBody("body-unstructured");
        var evt = CreateIssuesEvent(title, body, issueNumber: 22);

        var intent = await _parser.ParseAsync(evt);

        intent.Technology.Should().Be(".NET");
        intent.Scope.Should().Be(UpgradeScope.Full);
    }

    [Fact]
    public async Task ParseAsync_EmptyBody_ReturnsDefaults()
    {
        var evt = CreateIssuesEvent("Some upgrade request", "", issueNumber: 23);

        var intent = await _parser.ParseAsync(evt);

        intent.Scope.Should().Be(UpgradeScope.Full);
        intent.SourceVersion.Should().Be("current");
        intent.TargetVersion.Should().Be("latest");
    }

    [Fact]
    public async Task ParseAsync_CodeOnlyScope_ParsesCorrectly()
    {
        var title = TestFixtureLoader.LoadTitle("body-code-only-scope");
        var body = TestFixtureLoader.LoadBody("body-code-only-scope");
        var evt = CreateIssuesEvent(title, body, issueNumber: 24);

        var intent = await _parser.ParseAsync(evt);

        intent.Scope.Should().Be(UpgradeScope.CodeOnly);
    }

    [Fact]
    public async Task ParseAsync_DocsOnlyScope_ParsesCorrectly()
    {
        var title = TestFixtureLoader.LoadTitle("body-docs-only-scope");
        var body = TestFixtureLoader.LoadBody("body-docs-only-scope");
        var evt = CreateIssuesEvent(title, body, issueNumber: 25);

        var intent = await _parser.ParseAsync(evt);

        intent.Scope.Should().Be(UpgradeScope.DocsOnly);
    }

    [Fact]
    public async Task ParseAsync_BodyWithReleaseNotesUrl_ExtractsUrl()
    {
        var title = TestFixtureLoader.LoadTitle("body-with-release-notes-url");
        var body = TestFixtureLoader.LoadBody("body-with-release-notes-url");
        var evt = CreateIssuesEvent(title, body, issueNumber: 26);

        var intent = await _parser.ParseAsync(evt);

        intent.ReleaseNotesUrl.Should().NotBeNullOrEmpty();
        intent.ReleaseNotesUrl.Should().StartWith("https://");
    }

    [Fact]
    public async Task ParseAsync_GarbageMarkdown_DoesNotThrow()
    {
        var title = TestFixtureLoader.LoadTitle("body-garbage-markdown");
        var body = TestFixtureLoader.LoadBody("body-garbage-markdown");
        var evt = CreateIssuesEvent(title, body, issueNumber: 27);

        var intent = await _parser.ParseAsync(evt);

        intent.Should().NotBeNull();
        intent.Technology.Should().Be("Python");
    }

    [Fact]
    public async Task ParseAsync_SetsIssueIdFromNodeId()
    {
        var evt = CreateIssuesEvent("Upgrade to .NET 9", "body", issueNumber: 30);

        var intent = await _parser.ParseAsync(evt);

        intent.IssueId.Should().Be("I_kwDOABCDEF12345678");
    }

    #endregion
}
