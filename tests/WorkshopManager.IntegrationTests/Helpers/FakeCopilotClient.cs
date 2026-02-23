using WorkshopManager.Models;
using WorkshopManager.Services;

namespace WorkshopManager.IntegrationTests.Helpers;

/// <summary>
/// Configurable fake Copilot client for integration tests.
/// Allows per-file control over transformation responses.
/// </summary>
public class FakeCopilotClient : ICopilotClient
{
    private Func<string, CopilotContext, CopilotResponse> _handler;

    /// <summary>
    /// Creates a FakeCopilotClient with a default handler that returns content unchanged.
    /// </summary>
    public FakeCopilotClient()
    {
        _handler = (content, _) => new CopilotResponse(content, Success: true, ErrorMessage: null, TokensUsed: 0);
    }

    /// <summary>
    /// Set a custom handler that controls what the fake returns for each file.
    /// </summary>
    public void OnTransform(Func<string, CopilotContext, CopilotResponse> handler)
    {
        _handler = handler;
    }

    /// <summary>
    /// Track all calls made to TransformContentAsync for assertions.
    /// </summary>
    public List<(string Content, string SkillPath, CopilotContext Context)> Calls { get; } = [];

    public Task<CopilotResponse> TransformContentAsync(
        string content, string skillPromptPath, CopilotContext context, CancellationToken cancellationToken = default)
    {
        Calls.Add((content, skillPromptPath, context));
        var response = _handler(content, context);
        return Task.FromResult(response);
    }

    public Task<bool> ValidateConnectionAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(true);
}
