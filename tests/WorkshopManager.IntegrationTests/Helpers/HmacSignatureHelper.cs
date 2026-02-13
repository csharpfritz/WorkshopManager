using System.Security.Cryptography;
using System.Text;

namespace WorkshopManager.IntegrationTests.Helpers;

/// <summary>
/// Computes HMAC-SHA256 signatures matching GitHub's X-Hub-Signature-256 header format.
/// </summary>
public static class HmacSignatureHelper
{
    private const string SignaturePrefix = "sha256=";

    /// <summary>
    /// Computes a valid HMAC-SHA256 signature for the given payload and secret,
    /// matching the format GitHub sends in the X-Hub-Signature-256 header.
    /// </summary>
    public static string ComputeSignature(string payload, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(payloadBytes);

        return SignaturePrefix + Convert.ToHexStringLower(hash);
    }

    /// <summary>
    /// Creates a deliberately invalid signature for negative testing.
    /// The format is correct (sha256=hex) but the hash will not match.
    /// </summary>
    public static string CreateInvalidSignature()
    {
        return SignaturePrefix + new string('a', 64);
    }

    /// <summary>
    /// Creates a malformed signature string (wrong prefix) for format validation testing.
    /// </summary>
    public static string CreateMalformedSignature()
    {
        return "sha1=" + new string('b', 40);
    }

    /// <summary>
    /// Verifies that a given signature matches the expected HMAC-SHA256 of the payload.
    /// Uses fixed-time comparison to match GitHub's validation behavior.
    /// </summary>
    public static bool VerifySignature(string payload, string secret, string signature)
    {
        var expected = ComputeSignature(payload, secret);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(signature));
    }
}
