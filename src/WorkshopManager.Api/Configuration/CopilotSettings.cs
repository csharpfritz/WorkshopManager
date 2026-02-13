using System.ComponentModel.DataAnnotations;

namespace WorkshopManager.Configuration;

public class CopilotSettings
{
    public const string SectionName = "Copilot";

    [Required]
    public string ApiEndpoint { get; set; } = "https://api.githubcopilot.com";

    [Required]
    public string ApiKey { get; set; } = default!;

    public string Model { get; set; } = "claude-sonnet-4";

    public int MaxTokens { get; set; } = 4096;

    public int TimeoutSeconds { get; set; } = 120;
}
