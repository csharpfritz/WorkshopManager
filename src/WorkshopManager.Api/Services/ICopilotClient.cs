using WorkshopManager.Models;

namespace WorkshopManager.Services;

/// <summary>
/// Client for interacting with the GitHub Copilot API to transform workshop content.
/// </summary>
public interface ICopilotClient
{
    Task<CopilotResponse> TransformContentAsync(
        string content,
        string skillPromptPath,
        CopilotContext context,
        CancellationToken cancellationToken = default);

    Task<bool> ValidateConnectionAsync(CancellationToken cancellationToken = default);
}
