using Microsoft.Extensions.Logging;
using WorkshopManager.Models;

namespace WorkshopManager.Services;

/// <summary>
/// Transforms code files (CodeSample, ProjectFile, Configuration) through Copilot.
/// Processes files sequentially with per-file error isolation.
/// </summary>
public class CodeTransformationService : ICodeTransformationService
{
    private readonly IRepositoryContentProvider _contentProvider;
    private readonly ISkillResolver _skillResolver;
    private readonly ICopilotClient _copilotClient;
    private readonly ILogger<CodeTransformationService> _logger;

    public CodeTransformationService(
        IRepositoryContentProvider contentProvider,
        ISkillResolver skillResolver,
        ICopilotClient copilotClient,
        ILogger<CodeTransformationService> logger)
    {
        _contentProvider = contentProvider;
        _skillResolver = skillResolver;
        _copilotClient = copilotClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TransformationResult>> TransformAsync(
        IReadOnlyList<ContentItem> items,
        UpgradeIntent intent,
        WorkshopStructure structure,
        string repoFullName,
        string commitSha,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Transforming {Count} code items for {Tech} {From}→{To}",
            items.Count, intent.Technology, intent.SourceVersion, intent.TargetVersion);

        var results = new List<TransformationResult>(items.Count);

        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var content = await _contentProvider.GetFileContentAsync(
                    repoFullName, commitSha, item.Path, ct);

                var skillPath = _skillResolver.ResolveSkillPath(item.Type, intent.Scope);
                var context = BuildContext(item, intent, structure, repoFullName);

                var response = await _copilotClient.TransformContentAsync(
                    content, skillPath, context, ct);

                results.Add(new TransformationResult
                {
                    Path = item.Path,
                    ContentType = item.Type,
                    Success = response.Success,
                    TransformedContent = response.Success ? response.TransformedContent : null,
                    OriginalContent = content,
                    ErrorMessage = response.ErrorMessage,
                    TokensUsed = response.TokensUsed
                });

                if (response.Success)
                {
                    _logger.LogDebug(
                        "Transformed {Path} ({Tokens} tokens)",
                        item.Path, response.TokensUsed);
                }
                else
                {
                    _logger.LogWarning(
                        "Transformation failed for {Path}: {Error}",
                        item.Path, response.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to transform {Path}", item.Path);

                results.Add(new TransformationResult
                {
                    Path = item.Path,
                    ContentType = item.Type,
                    Success = false,
                    OriginalContent = string.Empty,
                    ErrorMessage = $"Unexpected error: {ex.Message}",
                    TokensUsed = 0
                });
            }
        }

        _logger.LogInformation(
            "Code transformation complete: {Succeeded} succeeded, {Failed} failed out of {Total}",
            results.Count(r => r.Success), results.Count(r => !r.Success), results.Count);

        return results;
    }

    private static CopilotContext BuildContext(
        ContentItem item,
        UpgradeIntent intent,
        WorkshopStructure structure,
        string repoFullName)
    {
        return new CopilotContext(
            RepositoryFullName: repoFullName,
            FilePath: item.Path,
            FromVersion: intent.SourceVersion,
            ToVersion: intent.TargetVersion,
            Technology: item.Technology ?? structure.Technology);
    }
}
