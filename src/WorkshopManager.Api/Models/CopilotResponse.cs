namespace WorkshopManager.Models;

public record CopilotResponse(
    string TransformedContent,
    bool Success,
    string? ErrorMessage,
    int TokensUsed);
