using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using WorkshopManager.IntegrationTests.Helpers;

namespace WorkshopManager.IntegrationTests;

public class WebhookEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string TestWebhookSecret = "test-webhook-secret-for-integration-tests";

    private readonly WebApplicationFactory<Program> _factory;

    public WebhookEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["GitHubApp:AppId"] = "12345",
                    ["GitHubApp:PrivateKey"] = "test-private-key",
                    ["GitHubApp:WebhookSecret"] = TestWebhookSecret,
                    ["GitHubApp:AppName"] = "workshop-manager[bot]",
                    ["Copilot:ApiEndpoint"] = "https://api.githubcopilot.com",
                    ["Copilot:ApiKey"] = "test-api-key"
                });
            });
        });
    }

    private WebhookTestClient CreateWebhookClient()
    {
        var httpClient = _factory.CreateClient();
        return new WebhookTestClient(httpClient, defaultSecret: TestWebhookSecret);
    }

    private static string LoadFixture(string name)
    {
        var fixturesPath = Path.Combine(GetProjectDirectory(), "Fixtures", $"{name}.json");
        return File.ReadAllText(fixturesPath);
    }

    private static string GetProjectDirectory()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (Directory.GetFiles(dir, "*.csproj").Length > 0)
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }
        throw new InvalidOperationException("Could not locate project directory.");
    }

    #region Health Check

    [Fact]
    public async Task HealthCheck_ReturnsOk()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/healthz");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region Webhook Signature Validation

    [Fact]
    public async Task Webhook_ValidSignature_Returns200()
    {
        var webhookClient = CreateWebhookClient();
        var payload = LoadFixture("webhook-issues-labeled");

        var response = await webhookClient.SendWebhookAsync("issues", payload);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Webhook_InvalidSignature_ReturnsError()
    {
        var webhookClient = CreateWebhookClient();
        var payload = LoadFixture("webhook-issues-labeled");

        var response = await webhookClient.SendWebhookWithInvalidSignatureAsync("issues", payload);

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Webhook_MissingSignature_ReturnsError()
    {
        var webhookClient = CreateWebhookClient();
        var payload = LoadFixture("webhook-issues-labeled");

        var response = await webhookClient.SendWebhookWithoutSignatureAsync("issues", payload);

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden);
    }

    #endregion

    #region Issues Webhook Processing

    [Fact]
    public async Task Webhook_IssuesWithUpgradeLabel_Returns200()
    {
        var webhookClient = CreateWebhookClient();
        var payload = LoadFixture("webhook-issues-labeled");

        var response = await webhookClient.SendWebhookAsync("issues", payload);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Webhook_IssuesAssignedToBot_Returns200()
    {
        var webhookClient = CreateWebhookClient();
        var payload = LoadFixture("webhook-issues-assigned-bot");

        var response = await webhookClient.SendWebhookAsync("issues", payload);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Webhook_IssuesDualTrigger_Returns200()
    {
        var webhookClient = CreateWebhookClient();
        var payload = LoadFixture("webhook-issues-dual-trigger");

        var response = await webhookClient.SendWebhookAsync("issues", payload);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Webhook_IssuesWithoutTrigger_Returns200()
    {
        var webhookClient = CreateWebhookClient();
        var payload = LoadFixture("webhook-issues-opened");

        var response = await webhookClient.SendWebhookAsync("issues", payload);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task Webhook_MalformedPayload_ReturnsError()
    {
        var webhookClient = CreateWebhookClient();
        var payload = LoadFixture("webhook-malformed");

        var response = await webhookClient.SendWebhookAsync("issues", payload);

        // Malformed JSON should cause a server-side processing error
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task Webhook_PullRequestEvent_Returns200()
    {
        var webhookClient = CreateWebhookClient();
        var payload = LoadFixture("webhook-pr-dependabot");

        var response = await webhookClient.SendWebhookAsync("pull_request", payload);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion
}
