using FluentAssertions;
using WorkshopManager.Services;
using WorkshopManager.UnitTests.Helpers;

namespace WorkshopManager.UnitTests;

/// <summary>
/// Tests for ManifestParser - validates valid .workshop.yml parsing, partial manifests,
/// invalid YAML handling (returns null, not throw), and manifests with modules.
/// </summary>
public class ManifestParserTests
{
    private readonly ManifestParser _parser;

    public ManifestParserTests()
    {
        _parser = new ManifestParser();
    }

    [Fact]
    public void Parse_WithFullManifest_ParsesAllFields()
    {
        // Arrange
        var yaml = File.ReadAllText("Fixtures/manifest-full.yml");

        // Act
        var manifest = _parser.Parse(yaml);

        // Assert
        manifest.Should().NotBeNull();
        manifest!.Name.Should().Be("Example Workshop");
        manifest.Description.Should().Be("A comprehensive .NET workshop");
        
        manifest.Technology.Should().NotBeNull();
        manifest.Technology!.Primary.Should().Be("dotnet");
        manifest.Technology.Version.Should().Be("8.0");
        manifest.Technology.Additional.Should().Contain("typescript");
        
        manifest.Structure.Should().NotBeNull();
        manifest.Structure!.Modules.Should().HaveCount(2);
        manifest.Structure.Shared.Should().Contain("shared/common");
        manifest.Structure.Exclude.Should().Contain("temp");
        manifest.Structure.Exclude.Should().Contain("*.bak");
    }

    [Fact]
    public void Parse_WithFullManifest_ParsesModulesCorrectly()
    {
        // Arrange
        var yaml = File.ReadAllText("Fixtures/manifest-full.yml");

        // Act
        var manifest = _parser.Parse(yaml);

        // Assert
        manifest.Should().NotBeNull();
        var modules = manifest!.Structure!.Modules;
        
        modules[0].Path.Should().Be("modules/module-01");
        modules[0].Code.Should().Be("src");
        modules[0].Docs.Should().Be("README.md");
        modules[0].Title.Should().Be("Getting Started");
        modules[0].Order.Should().Be(1);
        
        modules[1].Path.Should().Be("modules/module-02");
        modules[1].Code.Should().Be("src");
        modules[1].Docs.Should().Be("README.md");
        modules[1].Title.Should().Be("Advanced Topics");
        modules[1].Order.Should().Be(2);
    }

    [Fact]
    public void Parse_WithPartialManifest_ParsesAvailableFields()
    {
        // Arrange
        var yaml = File.ReadAllText("Fixtures/manifest-partial.yml");

        // Act
        var manifest = _parser.Parse(yaml);

        // Assert
        manifest.Should().NotBeNull();
        manifest!.Name.Should().Be("Partial Workshop");
        manifest.Technology.Should().NotBeNull();
        manifest.Technology!.Primary.Should().Be("node");
        manifest.Technology.Version.Should().BeNull();
        manifest.Structure.Should().BeNull();
    }

    [Fact]
    public void Parse_WithInvalidYaml_ReturnsNull()
    {
        // Arrange
        var yaml = File.ReadAllText("Fixtures/manifest-invalid.yml");

        // Act
        var manifest = _parser.Parse(yaml);

        // Assert
        manifest.Should().BeNull("because invalid YAML should return null, not throw");
    }

    [Fact]
    public void Parse_WithEmptyString_ReturnsNull()
    {
        // Arrange
        var yaml = string.Empty;

        // Act
        var manifest = _parser.Parse(yaml);

        // Assert
        manifest.Should().BeNull();
    }

    [Fact]
    public void Parse_WithWhitespaceOnly_ReturnsNull()
    {
        // Arrange
        var yaml = "   \n\t  \n  ";

        // Act
        var manifest = _parser.Parse(yaml);

        // Assert
        manifest.Should().BeNull();
    }

    [Fact]
    public void Parse_WithNullContent_ReturnsNull()
    {
        // Arrange
        string? yaml = null;

        // Act
        var manifest = _parser.Parse(yaml!);

        // Assert
        manifest.Should().BeNull();
    }

    [Fact]
    public void Parse_WithTechnologyOnly_ParsesTechnology()
    {
        // Arrange
        var yaml = @"
technology:
  primary: python
  version: '3.11'
  additional:
    - docker
    - kubernetes
";

        // Act
        var manifest = _parser.Parse(yaml);

        // Assert
        manifest.Should().NotBeNull();
        manifest!.Technology.Should().NotBeNull();
        manifest.Technology!.Primary.Should().Be("python");
        manifest.Technology.Version.Should().Be("3.11");
        manifest.Technology.Additional.Should().HaveCount(2);
        manifest.Technology.Additional.Should().Contain("docker");
        manifest.Technology.Additional.Should().Contain("kubernetes");
    }

    [Fact]
    public void Parse_WithStructureOnly_ParsesStructure()
    {
        // Arrange
        var yaml = @"
structure:
  modules:
    - path: labs/lab-01
      title: Introduction
      order: 1
  shared:
    - common
    - utils
  exclude:
    - temp
    - '*.tmp'
";

        // Act
        var manifest = _parser.Parse(yaml);

        // Assert
        manifest.Should().NotBeNull();
        manifest!.Structure.Should().NotBeNull();
        manifest.Structure!.Modules.Should().ContainSingle();
        manifest.Structure.Modules[0].Path.Should().Be("labs/lab-01");
        manifest.Structure.Modules[0].Title.Should().Be("Introduction");
        manifest.Structure.Modules[0].Order.Should().Be(1);
        manifest.Structure.Shared.Should().HaveCount(2);
        manifest.Structure.Exclude.Should().HaveCount(2);
    }

    [Fact]
    public void Parse_WithModuleMinimalFields_HandlesDefaults()
    {
        // Arrange
        var yaml = @"
structure:
  modules:
    - path: module-01
";

        // Act
        var manifest = _parser.Parse(yaml);

        // Assert
        manifest.Should().NotBeNull();
        var module = manifest!.Structure!.Modules.Should().ContainSingle().Subject;
        module.Path.Should().Be("module-01");
        module.Code.Should().BeNull();
        module.Docs.Should().BeNull();
        module.Title.Should().BeNull();
        module.Order.Should().BeNull();
    }

    [Fact]
    public void Parse_WithExtraUnknownFields_IgnoresThem()
    {
        // Arrange
        var yaml = @"
name: Test Workshop
unknownField: should be ignored
technology:
  primary: dotnet
  anotherUnknown: also ignored
";

        // Act
        var manifest = _parser.Parse(yaml);

        // Assert
        manifest.Should().NotBeNull();
        manifest!.Name.Should().Be("Test Workshop");
        manifest.Technology.Should().NotBeNull();
        manifest.Technology!.Primary.Should().Be("dotnet");
    }

    [Fact]
    public void Parse_WithComplexModulePaths_ParsesCorrectly()
    {
        // Arrange
        var yaml = @"
structure:
  modules:
    - path: exercises/chapter-01/section-a
      title: First Section
      code: src
      docs: instructions.md
      order: 1
    - path: exercises/chapter-02/section-b
      title: Second Section
      code: samples
      docs: guide.md
      order: 2
";

        // Act
        var manifest = _parser.Parse(yaml);

        // Assert
        manifest.Should().NotBeNull();
        var modules = manifest!.Structure!.Modules;
        modules.Should().HaveCount(2);
        modules[0].Path.Should().Be("exercises/chapter-01/section-a");
        modules[0].Code.Should().Be("src");
        modules[0].Docs.Should().Be("instructions.md");
        modules[1].Path.Should().Be("exercises/chapter-02/section-b");
        modules[1].Code.Should().Be("samples");
        modules[1].Docs.Should().Be("guide.md");
    }

    [Fact]
    public void Parse_WithEmptyModulesList_ParsesEmptyList()
    {
        // Arrange
        var yaml = @"
structure:
  modules: []
  shared: []
  exclude: []
";

        // Act
        var manifest = _parser.Parse(yaml);

        // Assert
        manifest.Should().NotBeNull();
        manifest!.Structure.Should().NotBeNull();
        manifest.Structure!.Modules.Should().BeEmpty();
        manifest.Structure.Shared.Should().BeEmpty();
        manifest.Structure.Exclude.Should().BeEmpty();
    }

    [Fact]
    public void Parse_CaseInsensitive_ParsesCorrectly()
    {
        // Arrange - Using PascalCase instead of camelCase
        var yaml = @"
Name: Test Workshop
Technology:
  Primary: dotnet
  Version: '8.0'
";

        // Act
        var manifest = _parser.Parse(yaml);

        // Assert - YamlDotNet with CamelCaseNamingConvention accepts PascalCase and converts it
        manifest.Should().NotBeNull("because YamlDotNet camelCase convention handles PascalCase inputs");
        manifest!.Name.Should().Be("Test Workshop");
        manifest.Technology.Should().NotBeNull();
        manifest.Technology!.Primary.Should().Be("dotnet");
    }

    [Fact]
    public void Parse_WithDescriptionOnly_ParsesDescription()
    {
        // Arrange
        var yaml = @"
name: Simple Workshop
description: This is a basic workshop for learning fundamentals
";

        // Act
        var manifest = _parser.Parse(yaml);

        // Assert
        manifest.Should().NotBeNull();
        manifest!.Name.Should().Be("Simple Workshop");
        manifest.Description.Should().Be("This is a basic workshop for learning fundamentals");
        manifest.Technology.Should().BeNull();
        manifest.Structure.Should().BeNull();
    }
}
