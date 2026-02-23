using Microsoft.Extensions.Logging;
using WorkshopManager.Models;

namespace WorkshopManager.Services;

/// <summary>
/// Analyzes workshop repositories using manifest-based or convention-based detection.
/// Implements hybrid fallback: checks for .workshop.yml first, then applies conventions for any gaps.
/// </summary>
public class WorkshopAnalyzer : IWorkshopAnalyzer
{
    private readonly IRepositoryContentProvider _contentProvider;
    private readonly FileClassifier _fileClassifier;
    private readonly TechnologyDetector _technologyDetector;
    private readonly IManifestParser _manifestParser;
    private readonly ILogger<WorkshopAnalyzer> _logger;

    public WorkshopAnalyzer(
        IRepositoryContentProvider contentProvider,
        FileClassifier fileClassifier,
        TechnologyDetector technologyDetector,
        IManifestParser manifestParser,
        ILogger<WorkshopAnalyzer> logger)
    {
        _contentProvider = contentProvider;
        _fileClassifier = fileClassifier;
        _technologyDetector = technologyDetector;
        _manifestParser = manifestParser;
        _logger = logger;
    }

    public async Task<WorkshopStructure> AnalyzeAsync(
        string repoFullName,
        string commitSha,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Analyzing workshop structure for {Repo} at commit {Sha}",
            repoFullName, commitSha);

        // 1. Get the full file tree
        var files = await _contentProvider.GetFileTreeAsync(repoFullName, commitSha, ct);
        _logger.LogDebug("Found {FileCount} files in repository", files.Count);

        // 2. Check for manifest
        var manifestFile = files.FirstOrDefault(f =>
            f.Path.Equals(".workshop.yml", StringComparison.OrdinalIgnoreCase));
        WorkshopManifest? manifest = null;

        if (manifestFile is not null)
        {
            _logger.LogDebug("Found manifest at .workshop.yml");
            // Note: Manifest parsing will be implemented in WI-10
            // For now, manifest remains null
        }

        // 3. Classify all files into ContentItems
        var items = _fileClassifier.ClassifyFiles(files, manifest?.Structure?.Exclude);
        _logger.LogDebug(
            "Classified {ItemCount} content items from {FileCount} files",
            items.Count, files.Count);

        // 4. Detect technology from project files
        var projectFiles = items.Where(i => i.Type == ContentItemType.ProjectFile).ToList();
        var (technology, version) = await _technologyDetector.DetectAsync(
            projectFiles, repoFullName, commitSha, ct);

        _logger.LogInformation(
            "Detected technology: {Technology} {Version}",
            technology ?? "unknown", version ?? "(no version)");

        // 5. Apply manifest overrides (when manifest parsing is implemented)
        if (manifest?.Technology is not null)
        {
            technology = manifest.Technology.Primary;
            version = manifest.Technology.Version ?? version;
            _logger.LogDebug("Applied manifest technology overrides");
        }

        // 6. Apply module grouping
        IReadOnlyList<ContentItem> groupedItems;
        if (manifest?.Structure?.Modules is { Count: > 0 } modules)
        {
            groupedItems = _fileClassifier.ApplyManifestGroups(items, modules);
            _logger.LogDebug("Applied manifest-defined module groups");
        }
        else
        {
            groupedItems = _fileClassifier.InferGroups(items);
            _logger.LogDebug("Inferred groups from directory structure");
        }

        // 7. Extract version references for project files
        var itemsWithVersions = new List<ContentItem>();
        foreach (var item in groupedItems)
        {
            if (item.Type == ContentItemType.ProjectFile)
            {
                var versionRefs = await _technologyDetector.ExtractVersionReferencesAsync(
                    item, repoFullName, commitSha, ct);
                itemsWithVersions.Add(item with { VersionReferences = versionRefs });
            }
            else
            {
                itemsWithVersions.Add(item);
            }
        }

        // 8. Determine strategy
        var strategy = (manifest, manifestFile) switch
        {
            (null, _) => DetectionStrategy.Convention,
            ({ Structure: not null, Technology: not null }, _) => DetectionStrategy.Manifest,
            _ => DetectionStrategy.Hybrid
        };

        // 9. Build diagnostics
        var diagnostics = new List<string>();
        if (technology is null)
            diagnostics.Add("No project files found - technology could not be determined");
        if (projectFiles.Count == 0)
            diagnostics.Add("No project files found in repository");
        if (items.Count == 0)
            diagnostics.Add("No recognizable content items found");

        var result = new WorkshopStructure
        {
            RootPath = "/",
            Technology = technology ?? "unknown",
            TechnologyVersion = version,
            Manifest = manifest,
            Items = itemsWithVersions,
            Strategy = strategy,
            Diagnostics = diagnostics
        };

        _logger.LogInformation(
            "Analysis complete: {ItemCount} items, strategy={Strategy}, tech={Tech} {Version}",
            result.Items.Count, result.Strategy, result.Technology, result.TechnologyVersion);

        return result;
    }
}
