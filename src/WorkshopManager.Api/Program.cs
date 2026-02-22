using Octokit.Webhooks;
using Octokit.Webhooks.AspNetCore;
using WorkshopManager.Configuration;
using WorkshopManager.Services;
using WorkshopManager.Webhooks;

var builder = WebApplication.CreateBuilder(args);

// Configuration — Options pattern with validation
builder.Services
    .AddOptions<GitHubAppOptions>()
    .BindConfiguration(GitHubAppOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<CopilotSettings>()
    .BindConfiguration(CopilotSettings.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Wire webhook secret into Octokit's options
builder.Services
    .AddOptions<GitHubWebhookOptions>()
    .Configure<Microsoft.Extensions.Options.IOptions<GitHubAppOptions>>((webhookOpts, appOpts) =>
    {
        webhookOpts.Secret = appOpts.Value.WebhookSecret;
    });

// Services — stubs for Phase 1, real implementations swapped in later
builder.Services.AddSingleton<IIssueParser, StubIssueParser>();

// Copilot Analysis Service (WI-12)
builder.Services.AddHttpClient();
builder.Services.AddSingleton<ISkillResolver, SkillResolver>();
builder.Services.AddSingleton<ICopilotClient, CopilotClient>();

builder.Services.AddSingleton<WebhookEventProcessor, WorkshopWebhookEventProcessor>();

// Workshop Structure Analysis (WI-09, WI-10)
builder.Services.AddSingleton<FileClassifier>();
builder.Services.AddSingleton<IRepositoryContentProvider, InMemoryContentProvider>();
builder.Services.AddSingleton<TechnologyDetector>();
builder.Services.AddSingleton<IManifestParser, ManifestParser>();
builder.Services.AddSingleton<IWorkshopAnalyzer, WorkshopAnalyzer>();
// Note: InMemoryContentProvider is a stub - will be replaced with GitHub API provider

// Health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/healthz");

app.UseRouting()
    .UseEndpoints(endpoints => endpoints.MapGitHubWebhooks());

app.Run();
