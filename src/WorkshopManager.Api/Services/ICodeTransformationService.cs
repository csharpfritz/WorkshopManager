using WorkshopManager.Models;

namespace WorkshopManager.Services;

/// <summary>
/// Transforms code files (source code, project files, config) using Copilot.
/// Processes files individually with per-file error isolation.
/// </summary>
public interface ICodeTransformationService
{
    /// <summary>
    /// Transform a batch of code-related content items.
    /// </summary>
    /// <param name="items">Content items to transform (CodeSample, ProjectFile, Configuration).</param>
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
