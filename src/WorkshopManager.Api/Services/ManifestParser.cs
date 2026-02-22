using WorkshopManager.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace WorkshopManager.Services;

/// <summary>
/// Parses .workshop.yml manifest files into WorkshopManifest objects.
/// </summary>
public interface IManifestParser
{
    /// <summary>
    /// Parse YAML content into a WorkshopManifest.
    /// </summary>
    /// <param name="yamlContent">The raw YAML content from .workshop.yml.</param>
    /// <returns>Parsed manifest, or null if content is invalid/empty.</returns>
    WorkshopManifest? Parse(string yamlContent);
}

/// <summary>
/// Implementation of IManifestParser using YamlDotNet.
/// </summary>
public class ManifestParser : IManifestParser
{
    private readonly IDeserializer _deserializer;

    public ManifestParser()
    {
        // Configure YamlDotNet deserializer with camelCase naming convention
        // to match the .workshop.yml schema (e.g., "primary" -> Primary)
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .WithCaseInsensitivePropertyMatching()
            .IgnoreUnmatchedProperties() // Gracefully handle extra fields
            .Build();
    }

    public WorkshopManifest? Parse(string yamlContent)
    {
        if (string.IsNullOrWhiteSpace(yamlContent))
        {
            return null;
        }

        try
        {
            // Deserialize into an intermediate DTO to handle required fields validation
            var dto = _deserializer.Deserialize<ManifestDto>(yamlContent);

            if (dto == null)
            {
                return null;
            }

            // Map DTO to domain model
            return new WorkshopManifest
            {
                Name = dto.Name,
                Description = dto.Description,
                Technology = MapTechnology(dto.Technology),
                Structure = MapStructure(dto.Structure)
            };
        }
        catch (Exception)
        {
            // In case of YAML parsing errors, return null
            // The analyzer will fall back to convention-based detection
            return null;
        }
    }

    private static ManifestTechnology? MapTechnology(TechnologyDto? dto)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.Primary))
        {
            return null;
        }

        return new ManifestTechnology
        {
            Primary = dto.Primary,
            Version = dto.Version,
            Additional = dto.Additional ?? []
        };
    }

    private static ManifestStructure? MapStructure(StructureDto? dto)
    {
        if (dto == null)
        {
            return null;
        }

        return new ManifestStructure
        {
            Modules = dto.Modules?.Select(MapModule).ToList() ?? [],
            Shared = dto.Shared ?? [],
            Exclude = dto.Exclude ?? []
        };
    }

    private static ManifestModule MapModule(ModuleDto dto)
    {
        return new ManifestModule
        {
            Path = dto.Path ?? string.Empty,
            Code = dto.Code,
            Docs = dto.Docs,
            Title = dto.Title,
            Order = dto.Order
        };
    }

    // DTOs for YamlDotNet deserialization (handles nullability more gracefully)
    private class ManifestDto
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public TechnologyDto? Technology { get; set; }
        public StructureDto? Structure { get; set; }
    }

    private class TechnologyDto
    {
        public string? Primary { get; set; }
        public string? Version { get; set; }
        public List<string>? Additional { get; set; }
    }

    private class StructureDto
    {
        public List<ModuleDto>? Modules { get; set; }
        public List<string>? Shared { get; set; }
        public List<string>? Exclude { get; set; }
    }

    private class ModuleDto
    {
        public string? Path { get; set; }
        public string? Code { get; set; }
        public string? Docs { get; set; }
        public string? Title { get; set; }
        public int? Order { get; set; }
    }
}
