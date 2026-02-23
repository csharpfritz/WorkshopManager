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

// Workshop Structure Analysis (WI-09, WI-10, WI-11)
builder.Services.AddSingleton<FileClassifier>();
builder.Services.AddScoped<IRepositoryContentProvider, GitHubContentProvider>();
builder.Services.AddScoped<TechnologyDetector>();
builder.Services.AddSingleton<IManifestParser, ManifestParser>();
builder.Services.AddScoped<IWorkshopAnalyzer, WorkshopAnalyzer>();

// PR Generation (WI-17)
builder.Services.AddScoped<IPullRequestService, PullRequestService>();

// Phase 3: Transformation & PR pipeline (WI-15, WI-16)
builder.Services.AddScoped<ICodeTransformationService, CodeTransformationService>();
builder.Services.AddScoped<IDocumentationTransformationService, DocumentationTransformationService>();
builder.Services.AddScoped<IUpgradeOrchestrator, UpgradeOrchestrator>();

// Health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/healthz");

app.UseRouting()
    .UseEndpoints(endpoints => endpoints.MapGitHubWebhooks());

app.Run();
