using System.Reflection;
using System.Text.Json;

namespace WorkshopManager.UnitTests.Helpers;

/// <summary>
/// Loads test fixture files (.md and .json) from the Fixtures directory.
/// Supports both file-system loading and embedded resource loading.
/// </summary>
public static class TestFixtureLoader
{
    private static readonly string FixturesBasePath = Path.Combine(
        GetProjectDirectory(),
        "Fixtures");

    /// <summary>
    /// Loads a markdown fixture file by name (without extension).
    /// </summary>
    /// <param name="fixtureName">Fixture name, e.g. "title-upgrade-from-to"</param>
    /// <returns>The raw markdown content.</returns>
    public static string LoadMarkdown(string fixtureName)
    {
        var path = Path.Combine(FixturesBasePath, $"{fixtureName}.md");
        if (!File.Exists(path))
            throw new FileNotFoundException(
                $"Fixture file not found: {path}. Available fixtures: {string.Join(", ", ListFixtures("*.md"))}");

        return File.ReadAllText(path);
    }

    /// <summary>
    /// Loads a JSON fixture file by name (without extension) and returns the raw string.
    /// </summary>
    /// <param name="fixtureName">Fixture name, e.g. "webhook-issues-labeled"</param>
    /// <returns>The raw JSON content.</returns>
    public static string LoadJsonString(string fixtureName)
    {
        var path = Path.Combine(FixturesBasePath, $"{fixtureName}.json");
        if (!File.Exists(path))
            throw new FileNotFoundException(
                $"Fixture file not found: {path}. Available fixtures: {string.Join(", ", ListFixtures("*.json"))}");

        return File.ReadAllText(path);
    }

    /// <summary>
    /// Loads and deserializes a JSON fixture file.
    /// </summary>
    /// <typeparam name="T">Target type for deserialization.</typeparam>
    /// <param name="fixtureName">Fixture name, e.g. "webhook-issues-labeled"</param>
    /// <param name="options">Optional JsonSerializerOptions. Uses web defaults if null.</param>
    /// <returns>The deserialized object.</returns>
    public static T LoadJson<T>(string fixtureName, JsonSerializerOptions? options = null)
    {
        var json = LoadJsonString(fixtureName);
        options ??= new JsonSerializerOptions(JsonSerializerDefaults.Web);

        return JsonSerializer.Deserialize<T>(json, options)
            ?? throw new InvalidOperationException($"Deserialization of fixture '{fixtureName}' returned null.");
    }

    /// <summary>
    /// Gets the first line of a markdown fixture (typically the issue title).
    /// </summary>
    public static string LoadTitle(string fixtureName)
    {
        var content = LoadMarkdown(fixtureName);
        using var reader = new StringReader(content);
        return reader.ReadLine()?.Trim() ?? string.Empty;
    }

    /// <summary>
    /// Gets everything after the first line of a markdown fixture (the issue body).
    /// </summary>
    public static string LoadBody(string fixtureName)
    {
        var content = LoadMarkdown(fixtureName);
        var firstNewline = content.IndexOf('\n');
        if (firstNewline < 0)
            return string.Empty;

        return content[(firstNewline + 1)..].TrimStart('\r', '\n');
    }

    /// <summary>
    /// Lists available fixture files matching the given pattern.
    /// </summary>
    public static IEnumerable<string> ListFixtures(string searchPattern = "*.*")
    {
        if (!Directory.Exists(FixturesBasePath))
            return Enumerable.Empty<string>();

        return Directory.GetFiles(FixturesBasePath, searchPattern)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => name is not null)!;
    }

    private static string GetProjectDirectory()
    {
        // Walk up from the bin output directory to find the project root
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (Directory.GetFiles(dir, "*.csproj").Length > 0)
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new InvalidOperationException(
            "Could not locate project directory. Ensure tests run from within the project structure.");
    }
}
