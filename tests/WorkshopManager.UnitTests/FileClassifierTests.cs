using FluentAssertions;
using WorkshopManager.Models;
using WorkshopManager.Services;

namespace WorkshopManager.UnitTests;

/// <summary>
/// Tests for FileClassifier - validates file classification by extension/path patterns,
/// directory pattern recognition, group assignment, and excluded paths.
/// </summary>
public class FileClassifierTests
{
    private readonly FileClassifier _classifier;

    public FileClassifierTests()
    {
        _classifier = new FileClassifier();
    }

    [Theory]
    [InlineData("src/Program.cs", ContentItemType.CodeSample)]
    [InlineData("src/App.fs", ContentItemType.CodeSample)]
    [InlineData("src/index.js", ContentItemType.CodeSample)]
    [InlineData("src/app.ts", ContentItemType.CodeSample)]
    [InlineData("src/component.tsx", ContentItemType.CodeSample)]
    [InlineData("src/main.py", ContentItemType.CodeSample)]
    [InlineData("src/main.go", ContentItemType.CodeSample)]
    [InlineData("scripts/deploy.sh", ContentItemType.CodeSample)]
    [InlineData("scripts/setup.ps1", ContentItemType.CodeSample)]
    public void ClassifyFiles_CodeSamples_ClassifiedCorrectly(string path, ContentItemType expectedType)
    {
        // Arrange
        var files = new[] { new RepositoryFile(path, "blob", 100) };

        // Act
        var items = _classifier.ClassifyFiles(files);

        // Assert
        items.Should().ContainSingle();
        items[0].Type.Should().Be(expectedType);
        items[0].Path.Should().Be(path);
    }

    [Theory]
    [InlineData("Workshop.csproj", ContentItemType.ProjectFile)]
    [InlineData("src/App.fsproj", ContentItemType.ProjectFile)]
    [InlineData("package.json", ContentItemType.ProjectFile)]
    [InlineData("requirements.txt", ContentItemType.ProjectFile)]
    [InlineData("pyproject.toml", ContentItemType.ProjectFile)]
    [InlineData("setup.py", ContentItemType.ProjectFile)]
    [InlineData("go.mod", ContentItemType.ProjectFile)]
    [InlineData("go.sum", ContentItemType.ProjectFile)]
    [InlineData("pom.xml", ContentItemType.ProjectFile)]
    [InlineData("build.gradle", ContentItemType.ProjectFile)]
    public void ClassifyFiles_ProjectFiles_ClassifiedCorrectly(string path, ContentItemType expectedType)
    {
        // Arrange
        var files = new[] { new RepositoryFile(path, "blob", 100) };

        // Act
        var items = _classifier.ClassifyFiles(files);

        // Assert
        items.Should().ContainSingle();
        items[0].Type.Should().Be(expectedType);
    }

    [Theory]
    [InlineData("README.md", ContentItemType.Documentation)]
    [InlineData("WORKSHOP.md", ContentItemType.Documentation)]
    [InlineData("docs/guide.md", ContentItemType.Documentation)]
    [InlineData("docs/tutorial.txt", ContentItemType.Documentation)]
    [InlineData("instructions/lab01.md", ContentItemType.Documentation)]
    public void ClassifyFiles_Documentation_ClassifiedCorrectly(string path, ContentItemType expectedType)
    {
        // Arrange
        var files = new[] { new RepositoryFile(path, "blob", 100) };

        // Act
        var items = _classifier.ClassifyFiles(files);

        // Assert
        items.Should().ContainSingle();
        items[0].Type.Should().Be(expectedType);
    }

    [Theory]
    [InlineData("Dockerfile", ContentItemType.Configuration)]
    [InlineData("docker-compose.yml", ContentItemType.Configuration)]
    [InlineData("docker-compose.yaml", ContentItemType.Configuration)]
    [InlineData(".devcontainer/devcontainer.json", ContentItemType.Configuration)]
    [InlineData("config.json", ContentItemType.Configuration)]
    [InlineData("appsettings.json", ContentItemType.Configuration)]
    [InlineData("settings.yml", ContentItemType.Configuration)]
    [InlineData("config.toml", ContentItemType.Configuration)]
    public void ClassifyFiles_Configuration_ClassifiedCorrectly(string path, ContentItemType expectedType)
    {
        // Arrange
        var files = new[] { new RepositoryFile(path, "blob", 100) };

        // Act
        var items = _classifier.ClassifyFiles(files);

        // Assert
        items.Should().ContainSingle();
        items[0].Type.Should().Be(expectedType);
    }

    [Theory]
    [InlineData("images/logo.png", ContentItemType.Asset)]
    [InlineData("assets/icon.svg", ContentItemType.Asset)]
    [InlineData("docs/screenshot.jpg", ContentItemType.Asset)]
    [InlineData("media/diagram.gif", ContentItemType.Asset)]
    public void ClassifyFiles_Assets_ClassifiedCorrectly(string path, ContentItemType expectedType)
    {
        // Arrange
        var files = new[] { new RepositoryFile(path, "blob", 100) };

        // Act
        var items = _classifier.ClassifyFiles(files);

        // Assert
        items.Should().ContainSingle();
        items[0].Type.Should().Be(expectedType);
    }

    [Theory]
    [InlineData(".git/config")]
    [InlineData("node_modules/package/index.js")]
    [InlineData("bin/Debug/net8.0/Workshop.dll")]
    [InlineData("obj/project.assets.json")]
    [InlineData(".vs/Workshop/v17/config")]
    [InlineData("__pycache__/module.pyc")]
    [InlineData(".workshop.yml")]
    public void ClassifyFiles_ExcludedPaths_AreFilteredOut(string path)
    {
        // Arrange
        var files = new[] { new RepositoryFile(path, "blob", 100) };

        // Act
        var items = _classifier.ClassifyFiles(files);

        // Assert
        items.Should().BeEmpty($"because {path} should be excluded");
    }

    [Fact]
    public void ClassifyFiles_WithManifestExcludes_FiltersAdditionalPaths()
    {
        // Arrange
        var files = new[]
        {
            new RepositoryFile("src/Program.cs", "blob", 100),
            new RepositoryFile("temp/scratch.cs", "blob", 100),
            new RepositoryFile("backup.bak", "blob", 100),
            new RepositoryFile("docs/guide.md", "blob", 100)
        };
        var excludePaths = new[] { "temp", "*.bak" };

        // Act
        var items = _classifier.ClassifyFiles(files, excludePaths);

        // Assert
        items.Should().HaveCount(2);
        items.Should().Contain(i => i.Path == "src/Program.cs");
        items.Should().Contain(i => i.Path == "docs/guide.md");
        items.Should().NotContain(i => i.Path.Contains("temp"));
        items.Should().NotContain(i => i.Path.Contains("backup"));
    }

    [Fact]
    public void ClassifyFiles_SortsProjectFilesFirst()
    {
        // Arrange
        var files = new[]
        {
            new RepositoryFile("README.md", "blob", 100),
            new RepositoryFile("src/Program.cs", "blob", 100),
            new RepositoryFile("Workshop.csproj", "blob", 100),
            new RepositoryFile("config.json", "blob", 100)
        };

        // Act
        var items = _classifier.ClassifyFiles(files);

        // Assert
        items[0].Type.Should().Be(ContentItemType.ProjectFile);
        items[0].Path.Should().Be("Workshop.csproj");
    }

    [Fact]
    public void InferGroups_WithModulesDirectory_AssignsCorrectGroups()
    {
        // Arrange
        var items = new[]
        {
            new ContentItem { Path = "modules/module-01/Program.cs", Type = ContentItemType.CodeSample },
            new ContentItem { Path = "modules/module-01/README.md", Type = ContentItemType.Documentation },
            new ContentItem { Path = "modules/module-02/App.cs", Type = ContentItemType.CodeSample },
            new ContentItem { Path = "README.md", Type = ContentItemType.Documentation }
        };

        // Act
        var grouped = _classifier.InferGroups(items);

        // Assert
        grouped.Should().Contain(i => i.Path == "modules/module-01/Program.cs" && i.Group == "module-01");
        grouped.Should().Contain(i => i.Path == "modules/module-01/README.md" && i.Group == "module-01");
        grouped.Should().Contain(i => i.Path == "modules/module-02/App.cs" && i.Group == "module-02");
        grouped.Should().Contain(i => i.Path == "README.md" && i.Group == null);
    }

    [Fact]
    public void InferGroups_WithLabsDirectory_AssignsCorrectGroups()
    {
        // Arrange
        var items = new[]
        {
            new ContentItem { Path = "labs/lab-01/exercise.cs", Type = ContentItemType.CodeSample },
            new ContentItem { Path = "labs/lab-02/exercise.cs", Type = ContentItemType.CodeSample },
            new ContentItem { Path = "src/Common.cs", Type = ContentItemType.CodeSample }
        };

        // Act
        var grouped = _classifier.InferGroups(items);

        // Assert
        grouped.Should().Contain(i => i.Path == "labs/lab-01/exercise.cs" && i.Group == "lab-01");
        grouped.Should().Contain(i => i.Path == "labs/lab-02/exercise.cs" && i.Group == "lab-02");
        grouped.Should().Contain(i => i.Path == "src/Common.cs" && i.Group == null);
    }

    [Fact]
    public void InferGroups_WithNumberedSections_AssignsCorrectGroups()
    {
        // Arrange
        var items = new[]
        {
            new ContentItem { Path = "chapter01-intro/Program.cs", Type = ContentItemType.CodeSample },
            new ContentItem { Path = "chapter02-advanced/App.cs", Type = ContentItemType.CodeSample },
            new ContentItem { Path = "step1-setup/config.json", Type = ContentItemType.Configuration }
        };

        // Act
        var grouped = _classifier.InferGroups(items);

        // Assert
        grouped.Should().Contain(i => i.Group == "chapter01-intro");
        grouped.Should().Contain(i => i.Group == "chapter02-advanced");
        grouped.Should().Contain(i => i.Group == "step1-setup");
    }

    [Fact]
    public void InferGroups_WithExercisesDirectory_AssignsCorrectGroups()
    {
        // Arrange
        var items = new[]
        {
            new ContentItem { Path = "exercises/ex-01/solution.cs", Type = ContentItemType.CodeSample },
            new ContentItem { Path = "exercises/ex-02/solution.cs", Type = ContentItemType.CodeSample }
        };

        // Act
        var grouped = _classifier.InferGroups(items);

        // Assert
        grouped.Should().Contain(i => i.Path == "exercises/ex-01/solution.cs" && i.Group == "ex-01");
        grouped.Should().Contain(i => i.Path == "exercises/ex-02/solution.cs" && i.Group == "ex-02");
    }

    [Fact]
    public void ApplyManifestGroups_AssignsGroupsFromManifestModules()
    {
        // Arrange
        var items = new[]
        {
            new ContentItem { Path = "modules/module-01/src/Program.cs", Type = ContentItemType.CodeSample },
            new ContentItem { Path = "modules/module-01/README.md", Type = ContentItemType.Documentation },
            new ContentItem { Path = "modules/module-02/src/App.cs", Type = ContentItemType.CodeSample },
            new ContentItem { Path = "shared/Common.cs", Type = ContentItemType.CodeSample }
        };
        var modules = new[]
        {
            new ManifestModule { Path = "modules/module-01", Title = "Module 1" },
            new ManifestModule { Path = "modules/module-02", Title = "Module 2" }
        };

        // Act
        var grouped = _classifier.ApplyManifestGroups(items, modules);

        // Assert
        grouped.Should().Contain(i => i.Path == "modules/module-01/src/Program.cs" && i.Group == "module-01");
        grouped.Should().Contain(i => i.Path == "modules/module-01/README.md" && i.Group == "module-01");
        grouped.Should().Contain(i => i.Path == "modules/module-02/src/App.cs" && i.Group == "module-02");
        grouped.Should().Contain(i => i.Path == "shared/Common.cs" && i.Group == null);
    }

    [Fact]
    public void ClassifyFiles_HandlesPathsWithBackslashes()
    {
        // Arrange
        var files = new[]
        {
            new RepositoryFile(@"src\Program.cs", "blob", 100),
            new RepositoryFile(@"modules\module-01\App.cs", "blob", 100)
        };

        // Act
        var items = _classifier.ClassifyFiles(files);

        // Assert
        items.Should().HaveCount(2);
        items.Should().AllSatisfy(i => i.Type.Should().Be(ContentItemType.CodeSample));
    }

    [Fact]
    public void ClassifyFiles_IgnoresUnrecognizedExtensions()
    {
        // Arrange
        var files = new[]
        {
            new RepositoryFile("data.dat", "blob", 100),
            new RepositoryFile("binary.bin", "blob", 100),
            new RepositoryFile("random.xyz", "blob", 100)
        };

        // Act
        var items = _classifier.ClassifyFiles(files);

        // Assert
        items.Should().BeEmpty("because unrecognized extensions are not classified");
    }

    [Fact]
    public void ClassifyFiles_CodeInDocsDirectory_StillMarkedAsDocumentation()
    {
        // Arrange
        var files = new[]
        {
            new RepositoryFile("docs/example.md", "blob", 100),
            new RepositoryFile("docs/notes.txt", "blob", 100)
        };

        // Act
        var items = _classifier.ClassifyFiles(files);

        // Assert
        items.Should().AllSatisfy(i => i.Type.Should().Be(ContentItemType.Documentation));
    }

    [Fact]
    public void ClassifyFiles_MultipleLevelsOfExcludedDirectory_AllFiltered()
    {
        // Arrange
        var files = new[]
        {
            new RepositoryFile("node_modules/pkg/src/index.js", "blob", 100),
            new RepositoryFile("node_modules/pkg/lib/module.js", "blob", 100),
            new RepositoryFile("src/valid.js", "blob", 100)
        };

        // Act
        var items = _classifier.ClassifyFiles(files);

        // Assert
        items.Should().ContainSingle();
        items[0].Path.Should().Be("src/valid.js");
    }
}
