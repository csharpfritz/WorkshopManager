using System.Net.Http.Headers;
using System.Text;

namespace WorkshopManager.IntegrationTests.Helpers;

/// <summary>
/// Wraps HttpClient to send requests with GitHub webhook headers
/// (X-GitHub-Event, X-Hub-Signature-256, X-GitHub-Delivery, etc.).
/// </summary>
public class WebhookTestClient
{
    private readonly HttpClient _httpClient;
    private readonly string _webhookEndpoint;
    private readonly string? _defaultSecret;

    /// <summary>
    /// Creates a new WebhookTestClient.
    /// </summary>
    /// <param name="httpClient">The HttpClient (typically from WebApplicationFactory).</param>
    /// <param name="webhookEndpoint">The webhook route, e.g. "/api/github/webhooks".</param>
    /// <param name="defaultSecret">Default webhook secret for HMAC signing. Pass null to skip signing.</param>
    public WebhookTestClient(HttpClient httpClient, string webhookEndpoint = "/api/github/webhooks", string? defaultSecret = null)
    {
        _httpClient = httpClient;
        _webhookEndpoint = webhookEndpoint;
        _defaultSecret = defaultSecret;
    }

    /// <summary>
    /// Sends a webhook payload with the correct GitHub headers.
    /// </summary>
    /// <param name="eventType">The GitHub event type (e.g. "issues", "pull_request", "issue_comment").</param>
    /// <param name="payload">The JSON payload body.</param>
    /// <param name="secret">Optional secret override. Uses default secret if null. Pass empty string to skip signing.</param>
    /// <returns>The HTTP response from the webhook endpoint.</returns>
    public async Task<HttpResponseMessage> SendWebhookAsync(string eventType, string payload, string? secret = null)
    {
        var effectiveSecret = secret ?? _defaultSecret;

        var request = new HttpRequestMessage(HttpMethod.Post, _webhookEndpoint)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

        request.Headers.Add("X-GitHub-Event", eventType);
        request.Headers.Add("X-GitHub-Delivery", Guid.NewGuid().ToString());
        request.Headers.Add("X-GitHub-Hook-ID", "123456789");
        request.Headers.Add("X-GitHub-Hook-Installation-Target-ID", "50000001");
        request.Headers.Add("X-GitHub-Hook-Installation-Target-Type", "integration");
        request.Headers.Add("User-Agent", "GitHub-Hookshot/test");

        if (!string.IsNullOrEmpty(effectiveSecret))
        {
            var signature = HmacSignatureHelper.ComputeSignature(payload, effectiveSecret);
            request.Headers.Add("X-Hub-Signature-256", signature);
        }

        return await _httpClient.SendAsync(request);
    }

    /// <summary>
    /// Sends a webhook with a deliberately invalid HMAC signature (for 401/403 testing).
    /// </summary>
    public async Task<HttpResponseMessage> SendWebhookWithInvalidSignatureAsync(string eventType, string payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, _webhookEndpoint)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

        request.Headers.Add("X-GitHub-Event", eventType);
        request.Headers.Add("X-GitHub-Delivery", Guid.NewGuid().ToString());
        request.Headers.Add("User-Agent", "GitHub-Hookshot/test");
        request.Headers.Add("X-Hub-Signature-256", HmacSignatureHelper.CreateInvalidSignature());

        return await _httpClient.SendAsync(request);
    }

    /// <summary>
    /// Sends a webhook with no signature header at all (for missing-signature testing).
    /// </summary>
    public async Task<HttpResponseMessage> SendWebhookWithoutSignatureAsync(string eventType, string payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, _webhookEndpoint)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

        request.Headers.Add("X-GitHub-Event", eventType);
        request.Headers.Add("X-GitHub-Delivery", Guid.NewGuid().ToString());
        request.Headers.Add("User-Agent", "GitHub-Hookshot/test");

        return await _httpClient.SendAsync(request);
    }
}
