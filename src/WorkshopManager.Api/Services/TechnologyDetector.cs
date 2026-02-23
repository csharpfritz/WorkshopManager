using System.Text.RegularExpressions;
using System.Xml.Linq;
using WorkshopManager.Models;

namespace WorkshopManager.Services;

/// <summary>
/// Detects primary technology and version from project files.
/// </summary>
public partial class TechnologyDetector
{
    private readonly IRepositoryContentProvider _contentProvider;

    public TechnologyDetector(IRepositoryContentProvider contentProvider)
    {
        _contentProvider = contentProvider;
    }

    // .NET TargetFramework patterns
    [GeneratedRegex(@"<TargetFramework>net(\d+)\.(\d+)</TargetFramework>", RegexOptions.IgnoreCase)]
    private static partial Regex DotNetTargetFrameworkPattern();

    [GeneratedRegex(@"<TargetFramework>netstandard(\d+)\.(\d+)</TargetFramework>", RegexOptions.IgnoreCase)]
    private static partial Regex DotNetStandardPattern();

    [GeneratedRegex(@"""sdk"":\s*\{\s*""version"":\s*""(\d+)\.(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex GlobalJsonSdkPattern();

    // Node.js engine version
    [GeneratedRegex(@"""node"":\s*"">=?(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex NodeEnginePattern();

    // Python requires-python
    [GeneratedRegex(@"requires-python\s*=\s*"">=?(\d+)\.(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex PythonVersionPattern();

    // Go version
    [GeneratedRegex(@"^go\s+(\d+)\.(\d+)", RegexOptions.Multiline)]
    private static partial Regex GoVersionPattern();

    // Java version
    [GeneratedRegex(@"<java\.version>(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex JavaVersionPattern();

    [GeneratedRegex(@"sourceCompatibility\s*=\s*'?(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex GradleJavaVersionPattern();

    /// <summary>
    /// Detect primary technology and version from project files.
    /// </summary>
    /// <param name="projectItems">ContentItems marked as ProjectFile type.</param>
    /// <param name="repoFullName">Repository name.</param>
    /// <param name="commitSha">Commit SHA.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Tuple of (technology, version). Both may be null if detection fails.</returns>
    public async Task<(string? Technology, string? Version)> DetectAsync(
        IEnumerable<ContentItem> projectItems,
        string repoFullName,
        string commitSha,
        CancellationToken ct = default)
    {
        var items = projectItems.ToList();

        // Priority order: .NET → Node.js → Python → Go → Java
        
        // 1. Check for .NET
        var dotnetItem = items.FirstOrDefault(i =>
            i.Path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
            i.Path.EndsWith(".fsproj", StringComparison.OrdinalIgnoreCase));
        
        if (dotnetItem is not null)
        {
            var content = await _contentProvider.GetFileContentAsync(
                repoFullName, commitSha, dotnetItem.Path, ct);
            var version = ExtractDotNetVersion(content);
            if (version is not null)
                return ("dotnet", version);
        }

        // Check global.json for .NET SDK version
        var globalJson = items.FirstOrDefault(i =>
            i.Path.Equals("global.json", StringComparison.OrdinalIgnoreCase));
        
        if (globalJson is not null)
        {
            var content = await _contentProvider.GetFileContentAsync(
                repoFullName, commitSha, globalJson.Path, ct);
            var version = ExtractDotNetSdkVersion(content);
            if (version is not null)
                return ("dotnet", version);
        }

        // 2. Check for Node.js
        var packageJson = items.FirstOrDefault(i =>
            i.Path.EndsWith("package.json", StringComparison.OrdinalIgnoreCase));
        
        if (packageJson is not null)
        {
            var content = await _contentProvider.GetFileContentAsync(
                repoFullName, commitSha, packageJson.Path, ct);
            var version = ExtractNodeVersion(content);
            if (version is not null)
                return ("node", version);
        }

        // 3. Check for Python
        var pyprojectToml = items.FirstOrDefault(i =>
            i.Path.EndsWith("pyproject.toml", StringComparison.OrdinalIgnoreCase));
        
        if (pyprojectToml is not null)
        {
            var content = await _contentProvider.GetFileContentAsync(
                repoFullName, commitSha, pyprojectToml.Path, ct);
            var version = ExtractPythonVersion(content);
            if (version is not null)
                return ("python", version);
        }

        // Check .python-version
        var pythonVersion = items.FirstOrDefault(i =>
            i.Path.Equals(".python-version", StringComparison.OrdinalIgnoreCase));
        
        if (pythonVersion is not null)
        {
            var content = await _contentProvider.GetFileContentAsync(
                repoFullName, commitSha, pythonVersion.Path, ct);
            var version = content.Trim();
            if (!string.IsNullOrWhiteSpace(version))
                return ("python", version);
        }

        // Check for Python by presence of requirements.txt
        var requirementsTxt = items.FirstOrDefault(i =>
            i.Path.EndsWith("requirements.txt", StringComparison.OrdinalIgnoreCase));
        
        if (requirementsTxt is not null)
            return ("python", null);

        // 4. Check for Go
        var goMod = items.FirstOrDefault(i =>
            i.Path.EndsWith("go.mod", StringComparison.OrdinalIgnoreCase));
        
        if (goMod is not null)
        {
            var content = await _contentProvider.GetFileContentAsync(
                repoFullName, commitSha, goMod.Path, ct);
            var version = ExtractGoVersion(content);
            if (version is not null)
                return ("go", version);
        }

        // 5. Check for Java
        var pomXml = items.FirstOrDefault(i =>
            i.Path.EndsWith("pom.xml", StringComparison.OrdinalIgnoreCase));
        
        if (pomXml is not null)
        {
            var content = await _contentProvider.GetFileContentAsync(
                repoFullName, commitSha, pomXml.Path, ct);
            var version = ExtractJavaVersion(content);
            if (version is not null)
                return ("java", version);
        }

        var buildGradle = items.FirstOrDefault(i =>
            i.Path.EndsWith("build.gradle", StringComparison.OrdinalIgnoreCase));
        
        if (buildGradle is not null)
        {
            var content = await _contentProvider.GetFileContentAsync(
                repoFullName, commitSha, buildGradle.Path, ct);
            var version = ExtractGradleJavaVersion(content);
            if (version is not null)
                return ("java", version);
        }

        return (null, null);
    }

    /// <summary>
    /// Extract version references from a project file.
    /// </summary>
    public async Task<IReadOnlyList<VersionReference>> ExtractVersionReferencesAsync(
        ContentItem item,
        string repoFullName,
        string commitSha,
        CancellationToken ct = default)
    {
        var references = new List<VersionReference>();

        if (item.Type != ContentItemType.ProjectFile)
            return references;

        var content = await _contentProvider.GetFileContentAsync(
            repoFullName, commitSha, item.Path, ct);

        // .NET projects
        if (item.Path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
            item.Path.EndsWith(".fsproj", StringComparison.OrdinalIgnoreCase))
        {
            var version = ExtractDotNetVersion(content);
            if (version is not null)
                references.Add(new VersionReference(".NET", version, item.Path));
        }

        // Node.js
        if (item.Path.EndsWith("package.json", StringComparison.OrdinalIgnoreCase))
        {
            var version = ExtractNodeVersion(content);
            if (version is not null)
                references.Add(new VersionReference("Node.js", version, item.Path));
        }

        // Python
        if (item.Path.EndsWith("pyproject.toml", StringComparison.OrdinalIgnoreCase))
        {
            var version = ExtractPythonVersion(content);
            if (version is not null)
                references.Add(new VersionReference("Python", version, item.Path));
        }

        // Go
        if (item.Path.EndsWith("go.mod", StringComparison.OrdinalIgnoreCase))
        {
            var version = ExtractGoVersion(content);
            if (version is not null)
                references.Add(new VersionReference("Go", version, item.Path));
        }

        // Java
        if (item.Path.EndsWith("pom.xml", StringComparison.OrdinalIgnoreCase))
        {
            var version = ExtractJavaVersion(content);
            if (version is not null)
                references.Add(new VersionReference("Java", version, item.Path));
        }

        if (item.Path.EndsWith("build.gradle", StringComparison.OrdinalIgnoreCase))
        {
            var version = ExtractGradleJavaVersion(content);
            if (version is not null)
                references.Add(new VersionReference("Java", version, item.Path));
        }

        return references;
    }

    private string? ExtractDotNetVersion(string content)
    {
        // Try TargetFramework first
        var match = DotNetTargetFrameworkPattern().Match(content);
        if (match.Success)
            return $"{match.Groups[1].Value}.{match.Groups[2].Value}";

        // Try netstandard
        match = DotNetStandardPattern().Match(content);
        if (match.Success)
            return $"standard{match.Groups[1].Value}.{match.Groups[2].Value}";

        return null;
    }

    private string? ExtractDotNetSdkVersion(string content)
    {
        var match = GlobalJsonSdkPattern().Match(content);
        if (match.Success)
            return $"{match.Groups[1].Value}.{match.Groups[2].Value}";

        return null;
    }

    private string? ExtractNodeVersion(string content)
    {
        var match = NodeEnginePattern().Match(content);
        if (match.Success)
            return match.Groups[1].Value;

        return null;
    }

    private string? ExtractPythonVersion(string content)
    {
        var match = PythonVersionPattern().Match(content);
        if (match.Success)
            return $"{match.Groups[1].Value}.{match.Groups[2].Value}";

        return null;
    }

    private string? ExtractGoVersion(string content)
    {
        var match = GoVersionPattern().Match(content);
        if (match.Success)
            return $"{match.Groups[1].Value}.{match.Groups[2].Value}";

        return null;
    }

    private string? ExtractJavaVersion(string content)
    {
        var match = JavaVersionPattern().Match(content);
        if (match.Success)
            return match.Groups[1].Value;

        return null;
    }

    private string? ExtractGradleJavaVersion(string content)
    {
        var match = GradleJavaVersionPattern().Match(content);
        if (match.Success)
            return match.Groups[1].Value;

        return null;
    }
}
