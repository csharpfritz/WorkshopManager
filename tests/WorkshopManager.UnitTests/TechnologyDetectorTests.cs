using FluentAssertions;
using WorkshopManager.Models;
using WorkshopManager.Services;
using WorkshopManager.UnitTests.Helpers;

namespace WorkshopManager.UnitTests;

/// <summary>
/// Tests for TechnologyDetector - validates version extraction from project files,
/// priority ordering (.NET → Node → Python → Go → Java), and version reference extraction.
/// </summary>
public class TechnologyDetectorTests
{
    private readonly InMemoryContentProvider _contentProvider;
    private readonly TechnologyDetector _detector;

    public TechnologyDetectorTests()
    {
        _contentProvider = new InMemoryContentProvider();
        _detector = new TechnologyDetector(_contentProvider);
    }

    [Fact]
    public async Task DetectAsync_WithDotNetCsproj_ExtractsVersion()
    {
        // Arrange
        var csprojContent = File.ReadAllText("Fixtures/sample.csproj.txt");
        _contentProvider.AddFile("Workshop.csproj", csprojContent);
        var projectItems = new[]
        {
            new ContentItem { Path = "Workshop.csproj", Type = ContentItemType.ProjectFile }
        };

        // Act
        var (tech, version) = await _detector.DetectAsync(projectItems, "owner/repo", "abc123");

        // Assert
        tech.Should().Be("dotnet");
        version.Should().Be("8.0");
    }

    [Fact]
    public async Task DetectAsync_WithNodePackageJson_ExtractsVersion()
    {
        // Arrange
        var packageJsonContent = File.ReadAllText("Fixtures/sample-package.json.txt");
        _contentProvider.AddFile("package.json", packageJsonContent);
        var projectItems = new[]
        {
            new ContentItem { Path = "package.json", Type = ContentItemType.ProjectFile }
        };

        // Act
        var (tech, version) = await _detector.DetectAsync(projectItems, "owner/repo", "abc123");

        // Assert
        tech.Should().Be("node");
        version.Should().Be("20");
    }

    [Fact]
    public async Task DetectAsync_WithPythonPyproject_ExtractsVersion()
    {
        // Arrange
        var pyprojectContent = File.ReadAllText("Fixtures/sample-pyproject.toml.txt");
        _contentProvider.AddFile("pyproject.toml", pyprojectContent);
        var projectItems = new[]
        {
            new ContentItem { Path = "pyproject.toml", Type = ContentItemType.ProjectFile }
        };

        // Act
        var (tech, version) = await _detector.DetectAsync(projectItems, "owner/repo", "abc123");

        // Assert
        tech.Should().Be("python");
        version.Should().Be("3.11");
    }

    [Fact]
    public async Task DetectAsync_WithPythonRequirementsTxt_DetectsTechWithoutVersion()
    {
        // Arrange
        _contentProvider.AddFile("requirements.txt", "flask==2.3.0\nrequests==2.31.0");
        var projectItems = new[]
        {
            new ContentItem { Path = "requirements.txt", Type = ContentItemType.ProjectFile }
        };

        // Act
        var (tech, version) = await _detector.DetectAsync(projectItems, "owner/repo", "abc123");

        // Assert
        tech.Should().Be("python");
        version.Should().BeNull();
    }

    [Fact]
    public async Task DetectAsync_WithGoMod_ExtractsVersion()
    {
        // Arrange
        var goModContent = File.ReadAllText("Fixtures/sample-go.mod.txt");
        _contentProvider.AddFile("go.mod", goModContent);
        var projectItems = new[]
        {
            new ContentItem { Path = "go.mod", Type = ContentItemType.ProjectFile }
        };

        // Act
        var (tech, version) = await _detector.DetectAsync(projectItems, "owner/repo", "abc123");

        // Assert
        tech.Should().Be("go");
        version.Should().Be("1.21");
    }

    [Fact]
    public async Task DetectAsync_WithJavaPomXml_ExtractsVersion()
    {
        // Arrange
        var pomXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<project>
    <properties>
        <java.version>17</java.version>
    </properties>
</project>";
        _contentProvider.AddFile("pom.xml", pomXml);
        var projectItems = new[]
        {
            new ContentItem { Path = "pom.xml", Type = ContentItemType.ProjectFile }
        };

        // Act
        var (tech, version) = await _detector.DetectAsync(projectItems, "owner/repo", "abc123");

        // Assert
        tech.Should().Be("java");
        version.Should().Be("17");
    }

    [Fact]
    public async Task DetectAsync_WithGradleBuild_ExtractsVersion()
    {
        // Arrange
        var buildGradle = @"
plugins {
    id 'java'
}

sourceCompatibility = '17'
";
        _contentProvider.AddFile("build.gradle", buildGradle);
        var projectItems = new[]
        {
            new ContentItem { Path = "build.gradle", Type = ContentItemType.ProjectFile }
        };

        // Act
        var (tech, version) = await _detector.DetectAsync(projectItems, "owner/repo", "abc123");

        // Assert
        tech.Should().Be("java");
        version.Should().Be("17");
    }

    [Fact]
    public async Task DetectAsync_PriorityOrder_DotNetBeforeNode()
    {
        // Arrange - Has both .NET and Node, .NET should win
        var csprojContent = File.ReadAllText("Fixtures/sample.csproj.txt");
        var packageJsonContent = File.ReadAllText("Fixtures/sample-package.json.txt");
        
        _contentProvider.AddFile("Workshop.csproj", csprojContent);
        _contentProvider.AddFile("package.json", packageJsonContent);
        
        var projectItems = new[]
        {
            new ContentItem { Path = "Workshop.csproj", Type = ContentItemType.ProjectFile },
            new ContentItem { Path = "package.json", Type = ContentItemType.ProjectFile }
        };

        // Act
        var (tech, version) = await _detector.DetectAsync(projectItems, "owner/repo", "abc123");

        // Assert
        tech.Should().Be("dotnet", "because .NET has priority over Node.js");
        version.Should().Be("8.0");
    }

    [Fact]
    public async Task DetectAsync_PriorityOrder_NodeBeforePython()
    {
        // Arrange - Has both Node and Python, Node should win
        var packageJsonContent = File.ReadAllText("Fixtures/sample-package.json.txt");
        var pyprojectContent = File.ReadAllText("Fixtures/sample-pyproject.toml.txt");
        
        _contentProvider.AddFile("package.json", packageJsonContent);
        _contentProvider.AddFile("pyproject.toml", pyprojectContent);
        
        var projectItems = new[]
        {
            new ContentItem { Path = "package.json", Type = ContentItemType.ProjectFile },
            new ContentItem { Path = "pyproject.toml", Type = ContentItemType.ProjectFile }
        };

        // Act
        var (tech, version) = await _detector.DetectAsync(projectItems, "owner/repo", "abc123");

        // Assert
        tech.Should().Be("node", "because Node has priority over Python");
        version.Should().Be("20");
    }

    [Fact]
    public async Task DetectAsync_PriorityOrder_PythonBeforeGo()
    {
        // Arrange - Has both Python and Go, Python should win
        var pyprojectContent = File.ReadAllText("Fixtures/sample-pyproject.toml.txt");
        var goModContent = File.ReadAllText("Fixtures/sample-go.mod.txt");
        
        _contentProvider.AddFile("pyproject.toml", pyprojectContent);
        _contentProvider.AddFile("go.mod", goModContent);
        
        var projectItems = new[]
        {
            new ContentItem { Path = "pyproject.toml", Type = ContentItemType.ProjectFile },
            new ContentItem { Path = "go.mod", Type = ContentItemType.ProjectFile }
        };

        // Act
        var (tech, version) = await _detector.DetectAsync(projectItems, "owner/repo", "abc123");

        // Assert
        tech.Should().Be("python", "because Python has priority over Go");
        version.Should().Be("3.11");
    }

    [Fact]
    public async Task DetectAsync_WithNoProjectFiles_ReturnsNull()
    {
        // Arrange
        var projectItems = Array.Empty<ContentItem>();

        // Act
        var (tech, version) = await _detector.DetectAsync(projectItems, "owner/repo", "abc123");

        // Assert
        tech.Should().BeNull();
        version.Should().BeNull();
    }

    [Fact]
    public async Task DetectAsync_WithDotNetStandard_ExtractsVersion()
    {
        // Arrange
        var csproj = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework>
  </PropertyGroup>
</Project>";
        _contentProvider.AddFile("Library.csproj", csproj);
        var projectItems = new[]
        {
            new ContentItem { Path = "Library.csproj", Type = ContentItemType.ProjectFile }
        };

        // Act
        var (tech, version) = await _detector.DetectAsync(projectItems, "owner/repo", "abc123");

        // Assert
        tech.Should().Be("dotnet");
        version.Should().Be("standard2.1");
    }

    [Fact]
    public async Task DetectAsync_WithGlobalJson_ExtractsSdkVersion()
    {
        // Arrange
        var globalJson = @"{
  ""sdk"": {
    ""version"": ""8.0.100""
  }
}";
        _contentProvider.AddFile("global.json", globalJson);
        var projectItems = new[]
        {
            new ContentItem { Path = "global.json", Type = ContentItemType.ProjectFile }
        };

        // Act
        var (tech, version) = await _detector.DetectAsync(projectItems, "owner/repo", "abc123");

        // Assert
        tech.Should().Be("dotnet");
        version.Should().Be("8.0");
    }

    [Fact]
    public async Task ExtractVersionReferencesAsync_WithDotNetProject_ReturnsVersionReference()
    {
        // Arrange
        var csprojContent = File.ReadAllText("Fixtures/sample.csproj.txt");
        _contentProvider.AddFile("Workshop.csproj", csprojContent);
        var item = new ContentItem { Path = "Workshop.csproj", Type = ContentItemType.ProjectFile };

        // Act
        var references = await _detector.ExtractVersionReferencesAsync(item, "owner/repo", "abc123");

        // Assert
        references.Should().ContainSingle();
        references[0].FrameworkOrRuntime.Should().Be(".NET");
        references[0].Version.Should().Be("8.0");
        references[0].Location.Should().Be("Workshop.csproj");
    }

    [Fact]
    public async Task ExtractVersionReferencesAsync_WithNodeProject_ReturnsVersionReference()
    {
        // Arrange
        var packageJsonContent = File.ReadAllText("Fixtures/sample-package.json.txt");
        _contentProvider.AddFile("package.json", packageJsonContent);
        var item = new ContentItem { Path = "package.json", Type = ContentItemType.ProjectFile };

        // Act
        var references = await _detector.ExtractVersionReferencesAsync(item, "owner/repo", "abc123");

        // Assert
        references.Should().ContainSingle();
        references[0].FrameworkOrRuntime.Should().Be("Node.js");
        references[0].Version.Should().Be("20");
        references[0].Location.Should().Be("package.json");
    }

    [Fact]
    public async Task ExtractVersionReferencesAsync_WithPythonProject_ReturnsVersionReference()
    {
        // Arrange
        var pyprojectContent = File.ReadAllText("Fixtures/sample-pyproject.toml.txt");
        _contentProvider.AddFile("pyproject.toml", pyprojectContent);
        var item = new ContentItem { Path = "pyproject.toml", Type = ContentItemType.ProjectFile };

        // Act
        var references = await _detector.ExtractVersionReferencesAsync(item, "owner/repo", "abc123");

        // Assert
        references.Should().ContainSingle();
        references[0].FrameworkOrRuntime.Should().Be("Python");
        references[0].Version.Should().Be("3.11");
    }

    [Fact]
    public async Task ExtractVersionReferencesAsync_WithGoProject_ReturnsVersionReference()
    {
        // Arrange
        var goModContent = File.ReadAllText("Fixtures/sample-go.mod.txt");
        _contentProvider.AddFile("go.mod", goModContent);
        var item = new ContentItem { Path = "go.mod", Type = ContentItemType.ProjectFile };

        // Act
        var references = await _detector.ExtractVersionReferencesAsync(item, "owner/repo", "abc123");

        // Assert
        references.Should().ContainSingle();
        references[0].FrameworkOrRuntime.Should().Be("Go");
        references[0].Version.Should().Be("1.21");
    }

    [Fact]
    public async Task ExtractVersionReferencesAsync_WithNonProjectFile_ReturnsEmpty()
    {
        // Arrange
        _contentProvider.AddFile("README.md", "# Workshop");
        var item = new ContentItem { Path = "README.md", Type = ContentItemType.Documentation };

        // Act
        var references = await _detector.ExtractVersionReferencesAsync(item, "owner/repo", "abc123");

        // Assert
        references.Should().BeEmpty();
    }

    [Fact]
    public async Task DetectAsync_WithPythonVersionFile_ExtractsVersion()
    {
        // Arrange
        _contentProvider.AddFile(".python-version", "3.11.5");
        var projectItems = new[]
        {
            new ContentItem { Path = ".python-version", Type = ContentItemType.ProjectFile }
        };

        // Act
        var (tech, version) = await _detector.DetectAsync(projectItems, "owner/repo", "abc123");

        // Assert
        tech.Should().Be("python");
        version.Should().Be("3.11.5");
    }

    [Fact]
    public async Task DetectAsync_WithMalformedProjectFile_ReturnsNullVersion()
    {
        // Arrange
        var malformedCsproj = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <InvalidTag>something</InvalidTag>
  </PropertyGroup>
</Project>";
        _contentProvider.AddFile("Workshop.csproj", malformedCsproj);
        var projectItems = new[]
        {
            new ContentItem { Path = "Workshop.csproj", Type = ContentItemType.ProjectFile }
        };

        // Act
        var (tech, version) = await _detector.DetectAsync(projectItems, "owner/repo", "abc123");

        // Assert
        tech.Should().BeNull("because no valid TargetFramework was found");
        version.Should().BeNull();
    }
}
