using WorkshopManager.Models;

namespace WorkshopManager.Services;

/// <summary>
/// Maps a content item type and upgrade scope to the correct skill prompt file.
/// </summary>
public interface ISkillResolver
{
    /// <summary>
    /// Returns the path to the skill prompt template for the given content type and scope.
    /// </summary>
    string ResolveSkillPath(ContentItemType contentType, UpgradeScope scope);
}
