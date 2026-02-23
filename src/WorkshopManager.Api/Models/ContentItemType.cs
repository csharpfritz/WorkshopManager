namespace WorkshopManager.Models;

/// <summary>
/// Classifies a content item in a workshop repository for skill routing.
/// </summary>
public enum ContentItemType
{
    /// <summary>Source code file (*.cs, *.ts, *.py, *.ps1, *.sh).</summary>
    CodeSample,

    /// <summary>Markdown or text documentation (*.md, *.txt).</summary>
    Documentation,

    /// <summary>Project/build file (*.csproj, *.sln, package.json, pyproject.toml).</summary>
    ProjectFile,

    /// <summary>Configuration file (*.json, *.yml, *.yaml, Dockerfile, devcontainer.json).</summary>
    Configuration,

    /// <summary>Static asset referenced by the workshop but not directly transformed.</summary>
    Asset
}
