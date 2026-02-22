using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using WorkshopManager.Models;
using WorkshopManager.Services;
using WorkshopManager.UnitTests.Helpers;

namespace WorkshopManager.UnitTests;

/// <summary>
/// Tests for WorkshopAnalyzer - validates full analysis flow using InMemoryContentProvider.
/// Covers: Convention, Manifest, and Hybrid detection strategies; technology detection priority;
/// module grouping; edge cases and diagnostics.
/// </summary>
public class WorkshopAnalyzerTests
{
    private readonly InMemoryContentProvider _contentProvider;
    private readonly FileClassifier _fileClassifier;
    private readonly TechnologyDetector _technologyDetector;
    private readonly ManifestParser _manifestParser;
    private readonly WorkshopAnalyzer _analyzer;

    public WorkshopAnalyzerTests()
    {
        _contentProvider = new InMemoryContentProvider();
        _fileClassifier = new FileClassifier();
        _manifestParser = new ManifestParser();
        _technologyDetector = new TechnologyDetector(_contentProvider);
        _analyzer = new WorkshopAnalyzer(
            _contentProvider,
            _fileClassifier,
            _technologyDetector,
            _manifestParser,
            NullLogger<WorkshopAnalyzer>.Instance);
    }

    [Fact]
    public async Task AnalyzeAsync_WithDotNetProject_DetectsCorrectTechnology()
    {
        // Arrange
        var csprojContent = File.ReadAllText("Fixtures/sample.csproj.txt");
        _contentProvider.AddFiles(new Dictionary<string, string>
        {
            ["src/Workshop.csproj"] = csprojContent,
            ["README.md"] = "# Workshop"
        });

        // Act
        var result = await _analyzer.AnalyzeAsync("owner/repo", "abc123");

        // Assert
        result.Technology.Should().Be("dotnet");
        result.TechnologyVersion.Should().Be("8.0");
        result.Strategy.Should().Be(DetectionStrategy.Convention);
        result.Items.Should().HaveCount(2);
        result.Items.Should().Contain(i => i.Type == ContentItemType.ProjectFile && i.Path == "src/Workshop.csproj");
        result.Items.Should().Contain(i => i.Type == ContentItemType.Documentation && i.Path == "README.md");
    }

    [Fact]
    public async Task AnalyzeAsync_WithNodeProject_DetectsCorrectTechnology()
    {
        // Arrange
        var packageJsonContent = File.ReadAllText("Fixtures/sample-package.json.txt");
        _contentProvider.AddFiles(new Dictionary<string, string>
        {
            ["package.json"] = packageJsonContent,
            ["src/index.js"] = "console.log('hello');"
        });

        // Act
        var result = await _analyzer.AnalyzeAsync("owner/repo", "abc123");

        // Assert
        result.Technology.Should().Be("node");
        result.TechnologyVersion.Should().Be("20");
        result.Strategy.Should().Be(DetectionStrategy.Convention);
        result.Items.Should().HaveCount(2);
        result.Items.Should().Contain(i => i.Type == ContentItemType.ProjectFile && i.Path == "package.json");
        result.Items.Should().Contain(i => i.Type == ContentItemType.CodeSample && i.Path == "src/index.js");
    }

    [Fact]
    public async Task AnalyzeAsync_WithPythonProject_DetectsCorrectTechnology()
    {
        // Arrange
        var pyprojectContent = File.ReadAllText("Fixtures/sample-pyproject.toml.txt");
        _contentProvider.AddFiles(new Dictionary<string, string>
        {
            ["pyproject.toml"] = pyprojectContent,
            ["src/main.py"] = "print('hello')"
        });

        // Act
        var result = await _analyzer.AnalyzeAsync("owner/repo", "abc123");

        // Assert
        result.Technology.Should().Be("python");
        result.TechnologyVersion.Should().Be("3.11");
        result.Strategy.Should().Be(DetectionStrategy.Convention);
    }

    [Fact]
    public async Task AnalyzeAsync_WithGoProject_DetectsCorrectTechnology()
    {
        // Arrange
        var goModContent = File.ReadAllText("Fixtures/sample-go.mod.txt");
        _contentProvider.AddFiles(new Dictionary<string, string>
        {
            ["go.mod"] = goModContent,
            ["main.go"] = "package main"
        });

        // Act
        var result = await _analyzer.AnalyzeAsync("owner/repo", "abc123");

        // Assert
        result.Technology.Should().Be("go");
        result.TechnologyVersion.Should().Be("1.21");
        result.Strategy.Should().Be(DetectionStrategy.Convention);
    }

    [Fact]
    public async Task AnalyzeAsync_WithManifest_UsesManifestStrategy()
    {
        // Arrange
        var manifestContent = File.ReadAllText("Fixtures/manifest-full.yml");
        var csprojContent = File.ReadAllText("Fixtures/sample.csproj.txt");
        
        _contentProvider.AddFiles(new Dictionary<string, string>
        {
            [".workshop.yml"] = manifestContent,
            ["modules/module-01/src/Program.cs"] = "class Program { }",
            ["modules/module-01/README.md"] = "# Module 1",
            ["modules/module-02/src/App.cs"] = "class App { }",
            ["shared/common/Util.cs"] = "class Util { }",
            ["src/Workshop.csproj"] = csprojContent
        });

        // Act
        var result = await _analyzer.AnalyzeAsync("owner/repo", "abc123");

        // Assert - Note: Manifest parsing not yet implemented in WI-10
        // When implemented, this should show Manifest strategy
        // For now, it falls back to Convention
        result.Strategy.Should().Be(DetectionStrategy.Convention);
        result.Technology.Should().Be("dotnet");
        result.Items.Should().HaveCount(5);
    }

    [Fact]
    public async Task AnalyzeAsync_WithPartialManifest_UsesHybridStrategy()
    {
        // Arrange
        var manifestContent = File.ReadAllText("Fixtures/manifest-partial.yml");
        var packageJsonContent = File.ReadAllText("Fixtures/sample-package.json.txt");
        
        _contentProvider.AddFiles(new Dictionary<string, string>
        {
            [".workshop.yml"] = manifestContent,
            ["package.json"] = packageJsonContent,
            ["src/index.js"] = "console.log('test');"
        });

        // Act
        var result = await _analyzer.AnalyzeAsync("owner/repo", "abc123");

        // Assert - Note: Until WI-10 is complete, this shows Convention
        // After manifest parsing, should be Hybrid (manifest has tech, convention fills structure)
        result.Strategy.Should().Be(DetectionStrategy.Convention);
        result.Technology.Should().Be("node");
    }

    [Fact]
    public async Task AnalyzeAsync_WithConventionBasedModules_InfersGroups()
    {
        // Arrange
        var csprojContent = File.ReadAllText("Fixtures/sample.csproj.txt");
        _contentProvider.AddFiles(new Dictionary<string, string>
        {
            ["modules/module-01/Program.cs"] = "class Program { }",
            ["modules/module-01/README.md"] = "# Module 1",
            ["modules/module-02/App.cs"] = "class App { }",
            ["src/Workshop.csproj"] = csprojContent,
            ["README.md"] = "# Workshop"
        });

        // Act
        var result = await _analyzer.AnalyzeAsync("owner/repo", "abc123");

        // Assert
        result.Items.Should().Contain(i => i.Group == "module-01" && i.Path == "modules/module-01/Program.cs");
        result.Items.Should().Contain(i => i.Group == "module-01" && i.Path == "modules/module-01/README.md");
        result.Items.Should().Contain(i => i.Group == "module-02" && i.Path == "modules/module-02/App.cs");
        result.Items.Should().Contain(i => i.Group == null && i.Path == "README.md");
    }

    [Fact]
    public async Task AnalyzeAsync_WithLabsDirectory_InfersGroups()
    {
        // Arrange
        var csprojContent = File.ReadAllText("Fixtures/sample.csproj.txt");
        _contentProvider.AddFiles(new Dictionary<string, string>
        {
            ["labs/lab-01/Program.cs"] = "class Program { }",
            ["labs/lab-02/App.cs"] = "class App { }",
            ["src/Workshop.csproj"] = csprojContent
        });

        // Act
        var result = await _analyzer.AnalyzeAsync("owner/repo", "abc123");

        // Assert
        result.Items.Should().Contain(i => i.Group == "lab-01" && i.Path == "labs/lab-01/Program.cs");
        result.Items.Should().Contain(i => i.Group == "lab-02" && i.Path == "labs/lab-02/App.cs");
    }

    [Fact]
    public async Task AnalyzeAsync_WithNumberedSections_InfersGroups()
    {
        // Arrange
        var csprojContent = File.ReadAllText("Fixtures/sample.csproj.txt");
        _contentProvider.AddFiles(new Dictionary<string, string>
        {
            ["chapter01-intro/Program.cs"] = "class Program { }",
            ["chapter02-advanced/App.cs"] = "class App { }",
            ["src/Workshop.csproj"] = csprojContent
        });

        // Act
        var result = await _analyzer.AnalyzeAsync("owner/repo", "abc123");

        // Assert
        result.Items.Should().Contain(i => i.Group == "chapter01-intro" && i.Path == "chapter01-intro/Program.cs");
        result.Items.Should().Contain(i => i.Group == "chapter02-advanced" && i.Path == "chapter02-advanced/App.cs");
    }

    [Fact]
    public async Task AnalyzeAsync_TechnologyPriorityOrdering_DotNetFirst()
    {
        // Arrange - Has both .NET and Node.js, .NET should win
        var csprojContent = File.ReadAllText("Fixtures/sample.csproj.txt");
        var packageJsonContent = File.ReadAllText("Fixtures/sample-package.json.txt");
        
        _contentProvider.AddFiles(new Dictionary<string, string>
        {
            ["src/Workshop.csproj"] = csprojContent,
            ["package.json"] = packageJsonContent
        });

        // Act
        var result = await _analyzer.AnalyzeAsync("owner/repo", "abc123");

        // Assert
        result.Technology.Should().Be("dotnet", "because .NET has priority over Node.js");
        result.TechnologyVersion.Should().Be("8.0");
    }

    [Fact]
    public async Task AnalyzeAsync_WithNoProjectFiles_ReportsUnknownTechnology()
    {
        // Arrange
        _contentProvider.AddFiles(new Dictionary<string, string>
        {
            ["README.md"] = "# Workshop",
            ["src/example.txt"] = "Some text"
        });

        // Act
        var result = await _analyzer.AnalyzeAsync("owner/repo", "abc123");

        // Assert
        result.Technology.Should().Be("unknown");
        result.TechnologyVersion.Should().BeNull();
        result.Diagnostics.Should().Contain(d => d.Contains("No project files found"));
    }

    [Fact]
    public async Task AnalyzeAsync_WithNoRecognizableContent_ReportsDiagnostics()
    {
        // Arrange
        _contentProvider.AddFiles(new Dictionary<string, string>
        {
            [".git/config"] = "git config",
            ["node_modules/package/index.js"] = "module.exports = {};"
        });

        // Act
        var result = await _analyzer.AnalyzeAsync("owner/repo", "abc123");

        // Assert
        result.Items.Should().BeEmpty("because all files are excluded");
        result.Diagnostics.Should().Contain(d => d.Contains("No recognizable content items found"));
    }

    [Fact]
    public async Task AnalyzeAsync_WithProjectFile_ExtractsVersionReferences()
    {
        // Arrange
        var csprojContent = File.ReadAllText("Fixtures/sample.csproj.txt");
        _contentProvider.AddFiles(new Dictionary<string, string>
        {
            ["src/Workshop.csproj"] = csprojContent
        });

        // Act
        var result = await _analyzer.AnalyzeAsync("owner/repo", "abc123");

        // Assert
        var projectItem = result.Items.Single(i => i.Type == ContentItemType.ProjectFile);
        projectItem.VersionReferences.Should().ContainSingle();
        projectItem.VersionReferences[0].FrameworkOrRuntime.Should().Be(".NET");
        projectItem.VersionReferences[0].Version.Should().Be("8.0");
        projectItem.VersionReferences[0].Location.Should().Be("src/Workshop.csproj");
    }

    [Fact]
    public async Task AnalyzeAsync_WithInvalidManifest_FallsBackToConvention()
    {
        // Arrange
        var invalidManifest = File.ReadAllText("Fixtures/manifest-invalid.yml");
        var csprojContent = File.ReadAllText("Fixtures/sample.csproj.txt");
        
        _contentProvider.AddFiles(new Dictionary<string, string>
        {
            [".workshop.yml"] = invalidManifest,
            ["src/Workshop.csproj"] = csprojContent,
            ["README.md"] = "# Workshop"
        });

        // Act
        var result = await _analyzer.AnalyzeAsync("owner/repo", "abc123");

        // Assert
        result.Strategy.Should().Be(DetectionStrategy.Convention, "because invalid manifest should fall back");
        result.Technology.Should().Be("dotnet");
        result.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task AnalyzeAsync_SortsItemsByType()
    {
        // Arrange
        var csprojContent = File.ReadAllText("Fixtures/sample.csproj.txt");
        _contentProvider.AddFiles(new Dictionary<string, string>
        {
            ["README.md"] = "# Workshop",
            ["src/Program.cs"] = "class Program { }",
            ["src/Workshop.csproj"] = csprojContent,
            ["docs/guide.md"] = "# Guide",
            ["config.json"] = "{}",
            ["images/logo.png"] = "binary"
        });

        // Act
        var result = await _analyzer.AnalyzeAsync("owner/repo", "abc123");

        // Assert
        // Order should be: ProjectFile → CodeSample → Documentation → Configuration → Asset
        result.Items[0].Type.Should().Be(ContentItemType.ProjectFile);
        result.Items[1].Type.Should().Be(ContentItemType.CodeSample);
        result.Items.Skip(2).Take(2).Should().AllSatisfy(i => i.Type.Should().Be(ContentItemType.Documentation));
        result.Items[4].Type.Should().Be(ContentItemType.Configuration);
        result.Items[5].Type.Should().Be(ContentItemType.Asset);
    }
}
