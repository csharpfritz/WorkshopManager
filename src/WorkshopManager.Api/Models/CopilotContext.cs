namespace WorkshopManager.Models;

public record CopilotContext(
    string RepositoryFullName,
    string FilePath,
    string FromVersion,
    string ToVersion,
    string Technology);
