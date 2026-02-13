using System.Text.Json;

namespace WorkshopManager.IntegrationTests.Helpers;

/// <summary>
/// Loads test fixture files (.json) from the IntegrationTests Fixtures directory.
/// </summary>
public static class TestFixtureLoader
{
    private static readonly string FixturesBasePath = Path.Combine(
        GetProjectDirectory(),
        "Fixtures");

    /// <summary>
    /// Loads a JSON fixture file by name (without extension) and returns the raw string.
    /// </summary>
    public static string LoadJsonString(string fixtureName)
    {
        var path = Path.Combine(FixturesBasePath, $"{fixtureName}.json");
        if (!File.Exists(path))
            throw new FileNotFoundException(
                $"Fixture file not found: {path}. Available: {string.Join(", ", ListFixtures("*.json"))}");

        return File.ReadAllText(path);
    }

    /// <summary>
    /// Loads and deserializes a JSON fixture file.
    /// </summary>
    public static T LoadJson<T>(string fixtureName, JsonSerializerOptions? options = null)
    {
        var json = LoadJsonString(fixtureName);
        options ??= new JsonSerializerOptions(JsonSerializerDefaults.Web);

        return JsonSerializer.Deserialize<T>(json, options)
            ?? throw new InvalidOperationException($"Deserialization of fixture '{fixtureName}' returned null.");
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
