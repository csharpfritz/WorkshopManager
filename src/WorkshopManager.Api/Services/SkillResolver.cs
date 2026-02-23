using WorkshopManager.Models;

namespace WorkshopManager.Services;

/// <summary>
/// Routes content items to the appropriate Copilot skill prompt based on type and scope.
/// Skills are embedded as content files in the Skills/ directory.
/// </summary>
public class SkillResolver : ISkillResolver
{
    private const string SkillsBasePath = "Skills";

    private static readonly Dictionary<ContentItemType, string> SkillFiles = new()
    {
        [ContentItemType.CodeSample] = "upgrade-code-sample.md",
        [ContentItemType.Documentation] = "upgrade-documentation.md",
        [ContentItemType.ProjectFile] = "upgrade-project-file.md",
        [ContentItemType.Configuration] = "upgrade-project-file.md"
    };

    /// <inheritdoc />
    public string ResolveSkillPath(ContentItemType contentType, UpgradeScope scope)
    {
        // Analysis scope always routes to the breaking-changes skill regardless of content type
        if (scope == UpgradeScope.Incremental)
        {
            return Path.Combine(SkillsBasePath, "analyze-breaking-changes.md");
        }

        if (SkillFiles.TryGetValue(contentType, out var fileName))
        {
            return Path.Combine(SkillsBasePath, fileName);
        }

        throw new ArgumentOutOfRangeException(
            nameof(contentType),
            contentType,
            $"No skill prompt registered for content type '{contentType}'.");
    }
}
