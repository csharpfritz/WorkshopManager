using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WorkshopManager.Configuration;
using WorkshopManager.Models;

namespace WorkshopManager.Services;

/// <summary>
/// Real Copilot client implementation that communicates with GitHub Copilot API.
/// Loads skill templates, hydrates placeholders, and transforms content.
/// </summary>
public partial class CopilotClient : ICopilotClient
{
    private readonly ILogger<CopilotClient> _logger;
    private readonly CopilotSettings _settings;
    private readonly ISkillResolver _skillResolver;
    private readonly HttpClient _httpClient;

    public CopilotClient(
        ILogger<CopilotClient> logger,
        IOptions<CopilotSettings> settings,
        ISkillResolver skillResolver,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _settings = settings.Value;
        _skillResolver = skillResolver;
        _httpClient = httpClientFactory.CreateClient("Copilot");
        
        // Configure HTTP client with base address and auth
        _httpClient.BaseAddress = new Uri(_settings.ApiEndpoint);
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_settings.ApiKey}");
        _httpClient.Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds);
    }

    /// <inheritdoc />
    public async Task<CopilotResponse> TransformContentAsync(
        string content,
        string skillPromptPath,
        CopilotContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Transforming content using skill {SkillPath} for {Tech} {From}→{To}",
                skillPromptPath, context.Technology, context.FromVersion, context.ToVersion);

            // Load and hydrate skill template
            var skillTemplate = await LoadSkillTemplateAsync(skillPromptPath, cancellationToken);
            var hydratedPrompt = HydratePlaceholders(skillTemplate, context);

            // Send to Copilot API
            var response = await SendCompletionRequestAsync(hydratedPrompt, content, cancellationToken);

            _logger.LogInformation(
                "Transformation completed. Tokens used: {TokensUsed}",
                response.TokensUsed);

            return response;
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogError(ex,
                "Skill file not found: {SkillPath}",
                skillPromptPath);

            return new CopilotResponse(
                TransformedContent: content,
                Success: false,
                ErrorMessage: $"Skill file not found: {skillPromptPath}",
                TokensUsed: 0);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
                "Failed to communicate with Copilot API: {Message}",
                ex.Message);

            return new CopilotResponse(
                TransformedContent: content,
                Success: false,
                ErrorMessage: $"Copilot API request failed: {ex.Message}",
                TokensUsed: 0);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex,
                "Copilot API request timed out after {Timeout}s",
                _settings.TimeoutSeconds);

            return new CopilotResponse(
                TransformedContent: content,
                Success: false,
                ErrorMessage: $"Request timed out after {_settings.TimeoutSeconds}s",
                TokensUsed: 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error during content transformation: {Message}",
                ex.Message);

            return new CopilotResponse(
                TransformedContent: content,
                Success: false,
                ErrorMessage: $"Transformation failed: {ex.Message}",
                TokensUsed: 0);
        }
    }

    /// <inheritdoc />
    public async Task<bool> ValidateConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Validating Copilot API connection");

            // Send a minimal ping request to verify credentials
            var request = new HttpRequestMessage(HttpMethod.Get, "/v1/health");
            var response = await _httpClient.SendAsync(request, cancellationToken);

            var isValid = response.IsSuccessStatusCode;
            
            if (isValid)
            {
                _logger.LogInformation("Copilot API connection validated successfully");
            }
            else
            {
                _logger.LogWarning(
                    "Copilot API connection validation failed with status {StatusCode}",
                    response.StatusCode);
            }

            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to validate Copilot API connection: {Message}",
                ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Loads a skill template file from disk.
    /// </summary>
    private async Task<string> LoadSkillTemplateAsync(string skillPath, CancellationToken cancellationToken)
    {
        // Skill path is relative to the application base directory
        var fullPath = Path.Combine(AppContext.BaseDirectory, skillPath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Skill template not found at {fullPath}", fullPath);
        }

        _logger.LogDebug("Loading skill template from {Path}", fullPath);
        return await File.ReadAllTextAsync(fullPath, cancellationToken);
    }

    /// <summary>
    /// Hydrates placeholders in the skill template with values from CopilotContext.
    /// Supports: {{technology}}, {{fromVersion}}, {{toVersion}}, {{releaseNotesUrl}}.
    /// </summary>
    private string HydratePlaceholders(string template, CopilotContext context)
    {
        var hydrated = template
            .Replace("{{technology}}", context.Technology, StringComparison.OrdinalIgnoreCase)
            .Replace("{{fromVersion}}", context.FromVersion, StringComparison.OrdinalIgnoreCase)
            .Replace("{{toVersion}}", context.ToVersion, StringComparison.OrdinalIgnoreCase)
            .Replace("{{releaseNotesUrl}}", "https://learn.microsoft.com/en-us/dotnet/core/whats-new", StringComparison.OrdinalIgnoreCase);

        _logger.LogDebug(
            "Hydrated skill template with {Tech} {From}→{To}",
            context.Technology, context.FromVersion, context.ToVersion);

        return hydrated;
    }

    /// <summary>
    /// Sends a completion request to the GitHub Copilot API.
    /// </summary>
    private async Task<CopilotResponse> SendCompletionRequestAsync(
        string prompt,
        string content,
        CancellationToken cancellationToken)
    {
        // Construct the request payload for Copilot API
        // Format follows GitHub Copilot SDK completion request structure
        var requestPayload = new
        {
            model = _settings.Model,
            max_tokens = _settings.MaxTokens,
            messages = new[]
            {
                new { role = "system", content = prompt },
                new { role = "user", content = $"Transform the following content:\n\n{content}" }
            }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/chat/completions")
        {
            Content = JsonContent.Create(requestPayload)
        };

        _logger.LogDebug(
            "Sending completion request to Copilot API (model={Model}, max_tokens={MaxTokens})",
            _settings.Model, _settings.MaxTokens);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<CompletionResponse>(cancellationToken);

        if (result?.Choices is not { Length: > 0 })
        {
            throw new InvalidOperationException("Copilot API returned empty response");
        }

        var transformedContent = result.Choices[0].Message.Content;
        var tokensUsed = result.Usage?.TotalTokens ?? 0;

        return new CopilotResponse(
            TransformedContent: transformedContent,
            Success: true,
            ErrorMessage: null,
            TokensUsed: tokensUsed);
    }

    /// <summary>
    /// Internal DTOs for Copilot API JSON serialization.
    /// </summary>
    private record CompletionResponse(
        CompletionChoice[] Choices,
        CompletionUsage? Usage);

    private record CompletionChoice(
        CompletionMessage Message);

    private record CompletionMessage(
        string Content);

    private record CompletionUsage(
        int TotalTokens);
}
