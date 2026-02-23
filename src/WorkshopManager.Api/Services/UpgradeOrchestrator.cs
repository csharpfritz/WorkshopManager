using Microsoft.Extensions.Logging;
using WorkshopManager.Models;

namespace WorkshopManager.Services;

/// <summary>
/// Orchestrates the complete workshop upgrade pipeline:
/// analyze → partition → transform code → transform docs → aggregate → PR.
/// </summary>
public class UpgradeOrchestrator : IUpgradeOrchestrator
{
    private readonly IWorkshopAnalyzer _analyzer;
    private readonly ICodeTransformationService _codeTransformer;
    private readonly IDocumentationTransformationService _docsTransformer;
    private readonly IPullRequestService _prService;
    private readonly IRepositoryContentProvider _contentProvider;
    private readonly ILogger<UpgradeOrchestrator> _logger;

    public UpgradeOrchestrator(
        IWorkshopAnalyzer analyzer,
        ICodeTransformationService codeTransformer,
        IDocumentationTransformationService docsTransformer,
        IPullRequestService prService,
        IRepositoryContentProvider contentProvider,
        ILogger<UpgradeOrchestrator> logger)
    {
        _analyzer = analyzer;
        _codeTransformer = codeTransformer;
        _docsTransformer = docsTransformer;
        _prService = prService;
        _contentProvider = contentProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<UpgradeResult> ExecuteAsync(
        UpgradeIntent intent,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Starting upgrade for {Repo}: {Tech} {From}→{To} (scope={Scope})",
            intent.RepoFullName, intent.Technology,
            intent.SourceVersion, intent.TargetVersion, intent.Scope);

        // 1. Get consistent commit reference (use file tree to resolve HEAD)
        string commitSha;
        try
        {
            var files = await _contentProvider.GetFileTreeAsync(intent.RepoFullName, "HEAD", ct);
            // Use HEAD as the commit reference — the content provider resolves it
            commitSha = "HEAD";
            _logger.LogDebug("Resolved repository tree with {FileCount} files", files.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to access repository {Repo}", intent.RepoFullName);
            return new UpgradeResult
            {
                Success = false,
                ErrorMessage = $"Repository access failed: {ex.Message}",
                FailedPhase = UpgradePhase.Analysis
            };
        }

        // 2. Analyze workshop structure
        WorkshopStructure structure;
        try
        {
            structure = await _analyzer.AnalyzeAsync(intent.RepoFullName, commitSha, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Workshop analysis failed for {Repo}", intent.RepoFullName);
            return new UpgradeResult
            {
                Success = false,
                ErrorMessage = $"Workshop analysis failed: {ex.Message}",
                FailedPhase = UpgradePhase.Analysis
            };
        }

        // 3. Partition items by transformation service
        var (codeItems, docItems) = PartitionItems(structure.Items, intent.Scope);

        _logger.LogInformation(
            "Partitioned {Total} items: {Code} code, {Docs} documentation",
            codeItems.Count + docItems.Count, codeItems.Count, docItems.Count);

        // 4. Transform
        var results = new List<TransformationResult>();

        if (codeItems.Count > 0)
        {
            var codeResults = await _codeTransformer.TransformAsync(
                codeItems, intent, structure, intent.RepoFullName, commitSha, ct);
            results.AddRange(codeResults);
        }

        if (docItems.Count > 0)
        {
            var docResults = await _docsTransformer.TransformAsync(
                docItems, intent, structure, intent.RepoFullName, commitSha, ct);
            results.AddRange(docResults);
        }

        // 5. Build summary
        var summary = new TransformationSummary
        {
            Results = results,
            Intent = intent,
            Structure = structure
        };

        _logger.LogInformation(
            "Transformation summary: {Succeeded} changed, {Failed} failed, {Unchanged} unchanged, {Tokens} tokens",
            summary.Succeeded.Count, summary.Failed.Count,
            summary.Unchanged.Count, summary.TotalTokensUsed);

        // 6. Bail if nothing changed (all failed or no diffs)
        if (!summary.HasAnyChanges)
        {
            return new UpgradeResult
            {
                Success = false,
                TransformationSummary = summary,
                ErrorMessage = summary.Failed.Count > 0
                    ? $"All {summary.Failed.Count} transformations failed."
                    : "No changes detected. Workshop may already be at target version.",
                FailedPhase = UpgradePhase.Transformation
            };
        }

        // 7. Generate PR
        try
        {
            var prResult = await _prService.CreatePullRequestAsync(summary, ct);
            return new UpgradeResult
            {
                Success = prResult.Success,
                PullRequest = prResult,
                TransformationSummary = summary,
                ErrorMessage = prResult.ErrorMessage,
                FailedPhase = prResult.Success ? null : UpgradePhase.PullRequestGeneration
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PR generation failed for {Repo}", intent.RepoFullName);
            return new UpgradeResult
            {
                Success = false,
                TransformationSummary = summary,
                ErrorMessage = $"PR generation failed: {ex.Message}",
                FailedPhase = UpgradePhase.PullRequestGeneration
            };
        }
    }

    private static (IReadOnlyList<ContentItem> Code, IReadOnlyList<ContentItem> Docs)
        PartitionItems(IReadOnlyList<ContentItem> items, UpgradeScope scope)
    {
        var transformable = items.Where(i => i.Type != ContentItemType.Asset);

        var filtered = scope switch
        {
            UpgradeScope.CodeOnly => transformable.Where(i =>
                i.Type is ContentItemType.CodeSample
                    or ContentItemType.ProjectFile
                    or ContentItemType.Configuration),
            UpgradeScope.DocsOnly => transformable.Where(i =>
                i.Type is ContentItemType.Documentation),
            _ => transformable // Full and Incremental process everything
        };

        var code = filtered
            .Where(i => i.Type is not ContentItemType.Documentation)
            .ToList();

        var docs = filtered
            .Where(i => i.Type is ContentItemType.Documentation)
            .ToList();

        return (code, docs);
    }
}
