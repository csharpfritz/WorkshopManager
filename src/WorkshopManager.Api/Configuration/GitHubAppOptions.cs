using System.ComponentModel.DataAnnotations;

namespace WorkshopManager.Configuration;

public class GitHubAppOptions
{
    public const string SectionName = "GitHubApp";

    [Required]
    public string AppId { get; set; } = default!;

    [Required]
    public string PrivateKey { get; set; } = default!;

    [Required]
    public string WebhookSecret { get; set; } = default!;

    public string AppName { get; set; } = "workshop-manager";
}
