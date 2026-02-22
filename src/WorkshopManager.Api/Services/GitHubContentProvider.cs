using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Octokit;
using WorkshopManager.Configuration;

namespace WorkshopManager.Services;

/// <summary>
/// IRepositoryContentProvider backed by the GitHub API via Octokit.
/// Authenticates as a GitHub App installation to access repository content.
/// </summary>
public class GitHubContentProvider : IRepositoryContentProvider
{
    private readonly GitHubAppOptions _options;
    private readonly ILogger<GitHubContentProvider> _logger;
    private GitHubClient? _installationClient;
    private string? _cachedRepoFullName;

    public GitHubContentProvider(
        IOptions<GitHubAppOptions> options,
        ILogger<GitHubContentProvider> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<RepositoryFile>> GetFileTreeAsync(
        string repoFullName, string commitSha, CancellationToken ct = default)
    {
        var (owner, repo) = ParseRepoFullName(repoFullName);
        var client = await GetInstallationClientAsync(owner, repo);

        try
        {
            var tree = await client.Git.Tree.GetRecursive(owner, repo, commitSha);

            if (tree.Truncated)
            {
                _logger.LogWarning(
                    "Repository tree for {Repo} at {Sha} was truncated. Some files may be missing.",
                    repoFullName, commitSha);
            }

            return tree.Tree
                .Select(item => new RepositoryFile(
                    item.Path,
                    item.Type.StringValue,
                    item.Size))
                .ToList();
        }
        catch (NotFoundException)
        {
            _logger.LogError(
                "Repository {Repo} not found or commit {Sha} does not exist.",
                repoFullName, commitSha);
            throw new InvalidOperationException(
                $"Repository '{repoFullName}' not found or commit '{commitSha}' does not exist.");
        }
        catch (RateLimitExceededException ex)
        {
            _logger.LogError(ex,
                "GitHub API rate limit exceeded. Resets at {Reset}.",
                ex.Reset);
            throw new InvalidOperationException(
                $"GitHub API rate limit exceeded. Try again after {ex.Reset}.", ex);
        }
        catch (ForbiddenException ex)
        {
            _logger.LogError(ex,
                "Access forbidden for {Repo}. This may indicate a secondary rate limit or insufficient permissions.",
                repoFullName);
            throw new InvalidOperationException(
                $"Access forbidden for '{repoFullName}'. Check App permissions or retry later.", ex);
        }
        catch (ApiException ex)
        {
            _logger.LogError(ex, "GitHub API error while fetching tree for {Repo}: {Message}",
                repoFullName, ex.Message);
            throw new InvalidOperationException(
                $"GitHub API error while fetching repository tree: {ex.Message}", ex);
        }
    }

    public async Task<string> GetFileContentAsync(
        string repoFullName, string commitSha, string path, CancellationToken ct = default)
    {
        var (owner, repo) = ParseRepoFullName(repoFullName);
        var client = await GetInstallationClientAsync(owner, repo);

        try
        {
            var rawContent = await client.Repository.Content.GetRawContentByRef(
                owner, repo, path, commitSha);
            return Encoding.UTF8.GetString(rawContent);
        }
        catch (NotFoundException)
        {
            _logger.LogError("File {Path} not found in {Repo} at {Sha}.",
                path, repoFullName, commitSha);
            throw new FileNotFoundException(
                $"File '{path}' not found in '{repoFullName}' at commit '{commitSha}'.");
        }
        catch (RateLimitExceededException ex)
        {
            _logger.LogError(ex,
                "GitHub API rate limit exceeded. Resets at {Reset}.",
                ex.Reset);
            throw new InvalidOperationException(
                $"GitHub API rate limit exceeded. Try again after {ex.Reset}.", ex);
        }
        catch (ForbiddenException ex)
        {
            _logger.LogError(ex,
                "Access forbidden when reading {Path} in {Repo}.",
                path, repoFullName);
            throw new InvalidOperationException(
                $"Access forbidden for '{path}' in '{repoFullName}'. Check App permissions or retry later.", ex);
        }
        catch (ApiException ex)
        {
            _logger.LogError(ex,
                "GitHub API error while fetching {Path} in {Repo}: {Message}",
                path, repoFullName, ex.Message);
            throw new InvalidOperationException(
                $"GitHub API error while fetching file content: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Obtains an authenticated GitHubClient for the App installation on the given repository.
    /// Caches the client within this scoped instance to avoid repeated token requests.
    /// </summary>
    private async Task<GitHubClient> GetInstallationClientAsync(string owner, string repo)
    {
        var repoFullName = $"{owner}/{repo}";

        if (_installationClient is not null && _cachedRepoFullName == repoFullName)
            return _installationClient;

        var jwt = GenerateJwt();
        var appClient = new GitHubClient(new ProductHeaderValue(_options.AppName))
        {
            Credentials = new Credentials(jwt, AuthenticationType.Bearer)
        };

        try
        {
            var installation = await appClient.GitHubApps.GetRepositoryInstallationForCurrent(owner, repo);
            var tokenResponse = await appClient.GitHubApps.CreateInstallationToken(installation.Id);

            _installationClient = new GitHubClient(new ProductHeaderValue(_options.AppName))
            {
                Credentials = new Credentials(tokenResponse.Token)
            };
            _cachedRepoFullName = repoFullName;

            _logger.LogDebug(
                "Obtained installation token for {Repo} (installation {InstallationId}).",
                repoFullName, installation.Id);

            return _installationClient;
        }
        catch (NotFoundException)
        {
            _logger.LogError("GitHub App is not installed on repository {Repo}.", repoFullName);
            throw new InvalidOperationException(
                $"GitHub App is not installed on repository '{repoFullName}'.");
        }
        catch (AuthorizationException ex)
        {
            _logger.LogError(ex,
                "Authentication failed for GitHub App {AppId}. Verify AppId and PrivateKey.",
                _options.AppId);
            throw new InvalidOperationException(
                "Authentication failed for GitHub App. Verify AppId and PrivateKey configuration.", ex);
        }
    }

    /// <summary>
    /// Generates a short-lived JWT for GitHub App authentication using RS256.
    /// </summary>
    private string GenerateJwt()
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(_options.PrivateKey);

        var now = DateTimeOffset.UtcNow;

        var headerBytes = JsonSerializer.SerializeToUtf8Bytes(
            new { alg = "RS256", typ = "JWT" });
        var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            iat = now.AddSeconds(-60).ToUnixTimeSeconds(),
            exp = now.AddMinutes(10).ToUnixTimeSeconds(),
            iss = _options.AppId
        });

        var header = Base64UrlEncode(headerBytes);
        var payload = Base64UrlEncode(payloadBytes);

        var dataToSign = Encoding.UTF8.GetBytes($"{header}.{payload}");
        var signature = Base64UrlEncode(
            rsa.SignData(dataToSign, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));

        return $"{header}.{payload}.{signature}";
    }

    private static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static (string Owner, string Repo) ParseRepoFullName(string repoFullName)
    {
        var parts = repoFullName.Split('/', 2);
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
            throw new ArgumentException(
                $"Invalid repository full name: '{repoFullName}'. Expected 'owner/repo' format.",
                nameof(repoFullName));
        return (parts[0], parts[1]);
    }
}
