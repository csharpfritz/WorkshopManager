using Microsoft.Extensions.Logging;
using WorkshopManager.Models;

namespace WorkshopManager.Services;

/// <summary>
/// Stub implementation that returns content unchanged.
/// Swap for real Copilot SDK client in Phase 2.
/// </summary>
public class StubCopilotClient : ICopilotClient
{
    private readonly ILogger<StubCopilotClient> _logger;

    public StubCopilotClient(ILogger<StubCopilotClient> logger)
    {
        _logger = logger;
    }

    public Task<CopilotResponse> TransformContentAsync(
        string content, string skillPromptPath, CopilotContext context, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "StubCopilotClient: returning content unchanged. " +
            "Skill={SkillPath}, Tech={Technology}, {Source}→{Target}",
            skillPromptPath, context.Technology, context.FromVersion, context.ToVersion);

        return Task.FromResult(new CopilotResponse(
            TransformedContent: content,
            Success: true,
            ErrorMessage: null,
            TokensUsed: 0));
    }

    public Task<bool> ValidateConnectionAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("StubCopilotClient: ValidateConnectionAsync returning true (stub)");
        return Task.FromResult(true);
    }
}
