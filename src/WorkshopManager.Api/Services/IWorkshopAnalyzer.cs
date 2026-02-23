using WorkshopManager.Models;

namespace WorkshopManager.Services;

/// <summary>
/// Analyzes a repository to discover its workshop structure.
/// Phase 2: operates on repository contents fetched via GitHub API.
/// </summary>
public interface IWorkshopAnalyzer
{
    /// <summary>
    /// Analyze a repository's workshop structure.
    /// </summary>
    /// <param name="repoFullName">Repository in "owner/repo" format (e.g., "csharpfritz/workshop-dotnet").</param>
    /// <param name="commitSha">The commit SHA to analyze (ensures consistency during analysis).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Discovered workshop structure.</returns>
    Task<WorkshopStructure> AnalyzeAsync(
        string repoFullName,
        string commitSha,
        CancellationToken ct = default);
}
