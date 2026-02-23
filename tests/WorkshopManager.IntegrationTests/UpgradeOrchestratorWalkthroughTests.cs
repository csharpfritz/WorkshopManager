using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WorkshopManager.IntegrationTests.Helpers;
using WorkshopManager.Models;
using WorkshopManager.Services;

namespace WorkshopManager.IntegrationTests;

/// <summary>
/// Walkthrough integration tests for the full UpgradeOrchestrator pipeline.
/// Designed to be stepped through in a debugger for manual validation.
/// 
/// Wiring: real DI from Program.cs via WebApplicationFactory, with
/// InMemoryContentProvider, FakeCopilotClient, and FakePullRequestService
/// replacing external dependencies.
/// </summary>
public class UpgradeOrchestratorWalkthroughTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public UpgradeOrchestratorWalkthroughTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    #region Realistic Workshop Content

    private const string CsprojContent = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <OutputType>Exe</OutputType>
            <TargetFramework>net8.0</TargetFramework>
            <ImplicitUsings>enable</ImplicitUsings>
            <Nullable>enable</Nullable>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.0" />
          </ItemGroup>
        </Project>
        """;

    private const string ProgramCsContent = """
        // .NET 8 Workshop Sample
        using Microsoft.Extensions.Hosting;

        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddHostedService<WorkerService>();

        var host = builder.Build();
        await host.RunAsync();

        public class WorkerService : BackgroundService
        {
            protected override async Task ExecuteAsync(CancellationToken stoppingToken)
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    Console.WriteLine($"Worker running at: {DateTimeOffset.Now}");
                    await Task.Delay(1000, stoppingToken);
                }
            }
        }
        """;

    private const string InstructionsMdContent = """
        # Workshop: Building Background Services with .NET 8

        ## Prerequisites
        - .NET 8 SDK installed
        - Visual Studio 2022 or VS Code

        ## Module 1: Creating a Worker Service
        In this module, you'll create a .NET 8 background worker using `Host.CreateApplicationBuilder`.

        ### Step 1
        Create a new project targeting `net8.0`:
        ```bash
        dotnet new worker -n MyWorker --framework net8.0
        ```

        ### Step 2
        Open `Program.cs` and examine the .NET 8 hosting pattern.

        ## References
        - [.NET 8 Documentation](https://learn.microsoft.com/dotnet/core/whats-new/dotnet-8)
        """;

    private const string WorkshopYmlContent = """
        name: Building Background Services
        description: Learn to build background services with .NET
        technology:
          primary: dotnet
          version: "8.0"
        structure:
          modules:
            - path: src
              code: "**/*.cs"
              docs: "**/*.md"
        """;

    // Transformed content returned by the fake Copilot client
    private const string CsprojTransformed = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <OutputType>Exe</OutputType>
            <TargetFramework>net10.0</TargetFramework>
            <ImplicitUsings>enable</ImplicitUsings>
            <Nullable>enable</Nullable>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="Microsoft.Extensions.Hosting" Version="10.0.0" />
          </ItemGroup>
        </Project>
        """;

    private const string ProgramCsTransformed = """
        // .NET 10 Workshop Sample
        using Microsoft.Extensions.Hosting;

        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddHostedService<WorkerService>();

        var host = builder.Build();
        await host.RunAsync();

        public class WorkerService : BackgroundService
        {
            protected override async Task ExecuteAsync(CancellationToken stoppingToken)
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    Console.WriteLine($"Worker running at: {DateTimeOffset.Now}");
                    await Task.Delay(1000, stoppingToken);
                }
            }
        }
        """;

    private const string InstructionsMdTransformed = """
        # Workshop: Building Background Services with .NET 10

        ## Prerequisites
        - .NET 10 SDK installed
        - Visual Studio 2026 or VS Code

        ## Module 1: Creating a Worker Service
        In this module, you'll create a .NET 10 background worker using `Host.CreateApplicationBuilder`.

        ### Step 1
        Create a new project targeting `net10.0`:
        ```bash
        dotnet new worker -n MyWorker --framework net10.0
        ```

        ### Step 2
        Open `Program.cs` and examine the .NET 10 hosting pattern.

        ## References
        - [.NET 10 Documentation](https://learn.microsoft.com/dotnet/core/whats-new/dotnet-10)
        """;

    #endregion

    #region Test Infrastructure

    private static UpgradeIntent CreateUpgradeIntent() => new(
        SourceVersion: ".NET 8",
        TargetVersion: ".NET 10",
        Technology: "dotnet",
        Scope: UpgradeScope.Full,
        IssueNumber: 1,
        IssueId: "I_abc123",
        RepoFullName: "test-owner/dotnet-workshop",
        RequestorLogin: "jeff",
        ReleaseNotesUrl: "https://learn.microsoft.com/dotnet/core/whats-new/dotnet-10");

    /// <summary>
    /// Build a scoped service provider from WebApplicationFactory with test overrides.
    /// Returns the scope (caller disposes), plus the fake instances for assertions.
    /// </summary>
    private (IServiceScope Scope, InMemoryContentProvider Content, FakeCopilotClient Copilot, FakePullRequestService PrService)
        CreateTestScope(Action<InMemoryContentProvider>? seedContent = null, Action<FakeCopilotClient>? configureCopilot = null)
    {
        var contentProvider = new InMemoryContentProvider();
        var copilotClient = new FakeCopilotClient();
        var prService = new FakePullRequestService();

        seedContent?.Invoke(contentProvider);
        configureCopilot?.Invoke(copilotClient);

        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["GitHubApp:AppId"] = "12345",
                    ["GitHubApp:PrivateKey"] = "test-private-key",
                    ["GitHubApp:WebhookSecret"] = "test-secret",
                    ["GitHubApp:AppName"] = "workshop-manager-test[bot]",
                    ["Copilot:ApiEndpoint"] = "https://api.githubcopilot.com",
                    ["Copilot:ApiKey"] = "test-api-key"
                });
            });
            builder.ConfigureServices(services =>
            {
                // Replace external dependencies with test fakes
                services.AddSingleton<InMemoryContentProvider>(contentProvider);
                services.AddSingleton<IRepositoryContentProvider>(sp => sp.GetRequiredService<InMemoryContentProvider>());
                services.AddSingleton<ICopilotClient>(copilotClient);
                services.AddSingleton<IPullRequestService>(prService);
            });
        });

        // Force the host to build so DI container is ready
        var scope = factory.Services.CreateScope();
        return (scope, contentProvider, copilotClient, prService);
    }

    private void SeedRealisticWorkshop(InMemoryContentProvider content)
    {
        content.AddFiles(new Dictionary<string, string>
        {
            ["src/MyWorker.csproj"] = CsprojContent,
            ["src/Program.cs"] = ProgramCsContent,
            ["instructions.md"] = InstructionsMdContent,
            [".workshop.yml"] = WorkshopYmlContent
        });
    }

    #endregion

    #region Test 1: Full Pipeline Success

    [Fact]
    public async Task ExecuteAsync_FullPipeline_TransformsAllFilesAndCreatesPR()
    {
        // Arrange — seed workshop content and configure Copilot to return upgraded content
        var (scope, content, copilot, prService) = CreateTestScope(
            seedContent: SeedRealisticWorkshop,
            configureCopilot: fake =>
            {
                fake.OnTransform((originalContent, context) =>
                {
                    // Route transformed content based on file path
                    var transformed = context.FilePath switch
                    {
                        "src/MyWorker.csproj" => CsprojTransformed,
                        "src/Program.cs" => ProgramCsTransformed,
                        "instructions.md" => InstructionsMdTransformed,
                        _ => originalContent // Manifest and other files pass through
                    };

                    return new CopilotResponse(
                        TransformedContent: transformed,
                        Success: true,
                        ErrorMessage: null,
                        TokensUsed: 150);
                });
            });

        using (scope)
        {
            var orchestrator = scope.ServiceProvider.GetRequiredService<IUpgradeOrchestrator>();
            var intent = CreateUpgradeIntent();

            // Act — run the full pipeline (set a breakpoint here to step through)
            var result = await orchestrator.ExecuteAsync(intent);

            // Assert — pipeline succeeded
            result.Success.Should().BeTrue($"Expected pipeline success but got: {result.ErrorMessage}");
            result.FailedPhase.Should().BeNull();

            // Assert — PR was created
            result.PullRequest.Should().NotBeNull();
            result.PullRequest!.Success.Should().BeTrue();
            result.PullRequest.PullRequestNumber.Should().Be(42);
            prService.CallCount.Should().Be(1);

            // Assert — transformation summary has results for each file
            var summary = result.TransformationSummary;
            summary.Should().NotBeNull();
            summary!.Results.Should().NotBeEmpty();

            // Assert — files that should change have HasChanges = true
            var csprojResult = summary.Results.FirstOrDefault(r => r.Path == "src/MyWorker.csproj");
            csprojResult.Should().NotBeNull();
            csprojResult!.HasChanges.Should().BeTrue("csproj should be transformed from net8.0 to net10.0");
            csprojResult.Success.Should().BeTrue();

            var programResult = summary.Results.FirstOrDefault(r => r.Path == "src/Program.cs");
            programResult.Should().NotBeNull();
            programResult!.HasChanges.Should().BeTrue("Program.cs should have .NET 10 patterns");
            programResult.Success.Should().BeTrue();

            var docsResult = summary.Results.FirstOrDefault(r => r.Path == "instructions.md");
            docsResult.Should().NotBeNull();
            docsResult!.HasChanges.Should().BeTrue("instructions should reference .NET 10");
            docsResult.Success.Should().BeTrue();

            // Assert — token counts are tracked
            summary.TotalTokensUsed.Should().BeGreaterThan(0, "Copilot token usage should be recorded");

            // Assert — Copilot was called for each transformable file (not the manifest)
            copilot.Calls.Should().HaveCountGreaterThanOrEqualTo(3,
                "Copilot should be called for csproj, Program.cs, and instructions.md");

            // Assert — all Copilot calls received correct version context
            copilot.Calls.Should().AllSatisfy(call =>
            {
                call.Context.FromVersion.Should().Be(".NET 8");
                call.Context.ToVersion.Should().Be(".NET 10");
            });
        }
    }

    #endregion

    #region Test 2: Partial Failure

    [Fact]
    public async Task ExecuteAsync_OneFileFailsTransformation_ReturnsPartialSuccess()
    {
        // Arrange — configure Copilot to fail on Program.cs but succeed on others
        var (scope, content, copilot, prService) = CreateTestScope(
            seedContent: SeedRealisticWorkshop,
            configureCopilot: fake =>
            {
                fake.OnTransform((originalContent, context) =>
                {
                    if (context.FilePath == "src/Program.cs")
                    {
                        // Simulate Copilot API failure for this file
                        return new CopilotResponse(
                            TransformedContent: originalContent,
                            Success: false,
                            ErrorMessage: "Copilot API rate limit exceeded",
                            TokensUsed: 0);
                    }

                    // Other files succeed with transformed content
                    var transformed = context.FilePath switch
                    {
                        "src/MyWorker.csproj" => CsprojTransformed,
                        "instructions.md" => InstructionsMdTransformed,
                        _ => originalContent
                    };

                    return new CopilotResponse(
                        TransformedContent: transformed,
                        Success: true,
                        ErrorMessage: null,
                        TokensUsed: 100);
                });
            });

        using (scope)
        {
            var orchestrator = scope.ServiceProvider.GetRequiredService<IUpgradeOrchestrator>();
            var intent = CreateUpgradeIntent();

            // Act
            var result = await orchestrator.ExecuteAsync(intent);

            // Assert — pipeline still succeeds (partial success creates PR with changed files)
            result.Success.Should().BeTrue("Pipeline should succeed when at least some files transform");

            var summary = result.TransformationSummary;
            summary.Should().NotBeNull();

            // Assert — we have both succeeded and failed results
            summary!.Failed.Should().NotBeEmpty("Program.cs transformation should have failed");
            summary.Succeeded.Should().NotBeEmpty("csproj and docs should have succeeded");

            // Assert — the failed file is Program.cs
            var failedFile = summary.Failed.First();
            failedFile.Path.Should().Be("src/Program.cs");
            failedFile.ErrorMessage.Should().Contain("rate limit");

            // Assert — succeeded files have HasChanges
            summary.Succeeded.Should().AllSatisfy(r => r.HasChanges.Should().BeTrue());

            // Assert — PR was still created (partial success creates PR)
            prService.CallCount.Should().Be(1);
            result.PullRequest.Should().NotBeNull();

            // Assert — the PR summary records the failure
            var prSummary = prService.LastSummary;
            prSummary.Should().NotBeNull();
            prSummary!.Failed.Count.Should().BeGreaterThanOrEqualTo(1);
        }
    }

    #endregion

    #region Test 3: Already at Target Version (No Changes)

    [Fact]
    public async Task ExecuteAsync_AlreadyAtTargetVersion_ReportsNoChanges()
    {
        // Arrange — Copilot returns content unchanged (already at target version)
        var (scope, content, copilot, prService) = CreateTestScope(
            seedContent: SeedRealisticWorkshop,
            configureCopilot: fake =>
            {
                fake.OnTransform((originalContent, context) =>
                {
                    // Return content unchanged — simulates "already at target version"
                    return new CopilotResponse(
                        TransformedContent: originalContent,
                        Success: true,
                        ErrorMessage: null,
                        TokensUsed: 50);
                });
            });

        using (scope)
        {
            var orchestrator = scope.ServiceProvider.GetRequiredService<IUpgradeOrchestrator>();
            var intent = CreateUpgradeIntent();

            // Act
            var result = await orchestrator.ExecuteAsync(intent);

            // Assert — pipeline reports failure because no changes were detected
            result.Success.Should().BeFalse("No changes means nothing to PR");
            result.FailedPhase.Should().Be(UpgradePhase.Transformation);
            result.ErrorMessage.Should().Contain("No changes detected");

            // Assert — transformation summary exists with all unchanged files
            var summary = result.TransformationSummary;
            summary.Should().NotBeNull();
            summary!.HasAnyChanges.Should().BeFalse("No files should have changed content");
            summary.Unchanged.Should().NotBeEmpty("All files should be in the Unchanged partition");
            summary.Failed.Should().BeEmpty("No files should have failed");

            // Assert — token usage still tracked even with no changes
            summary.TotalTokensUsed.Should().BeGreaterThan(0,
                "Copilot was still called even though content didn't change");

            // Assert — PR service was NOT called (no changes = no PR)
            prService.CallCount.Should().Be(0, "No PR should be created when nothing changed");
        }
    }

    #endregion
}
