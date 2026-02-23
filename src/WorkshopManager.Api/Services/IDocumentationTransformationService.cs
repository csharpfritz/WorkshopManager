using WorkshopManager.Models;

namespace WorkshopManager.Services;

/// <summary>
/// Transforms documentation files (markdown, prose) using Copilot.
/// Uses prose-specific prompting strategy distinct from code transformation.
/// </summary>
public interface IDocumentationTransformationService
{
    /// <summary>
    /// Transform a batch of documentation content items.
    /// </summary>
    /// <param name="items">Content items to transform (Documentation only).</param>
    /// <param name="intent">The upgrade intent driving transformation.</param>
    /// <param name="structure">Workshop structure for context.</param>
    /// <param name="repoFullName">Repository in "owner/repo" format.</param>
    /// <param name="commitSha">Commit SHA for consistent file reads.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Per-file transformation results.</returns>
    Task<IReadOnlyList<TransformationResult>> TransformAsync(
        IReadOnlyList<ContentItem> items,
        UpgradeIntent intent,
        WorkshopStructure structure,
        string repoFullName,
        string commitSha,
        CancellationToken ct = default);
}
