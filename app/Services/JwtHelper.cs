using System.Text;
using System.Text.Json;

namespace Muninn.Services;

/// <summary>
/// Lightweight JWT utilities for client-side expiry checks only.
/// This class never validates the signature — that is always the server's job.
/// Use it only to avoid making a network call when we already know the token is stale.
/// </summary>
public static class JwtHelper
{
    // Treat a token as expired 30 s before its actual exp, to avoid a race where we
    // send a request with a token that expires before the server receives it.
    private const long ClockSkewSeconds = 30;

    /// <summary>
    /// Returns true if the JWT is null, malformed, or within <see cref="ClockSkewSeconds"/>
    /// seconds of its expiry.  A return value of false means the token is structurally
    /// valid and still has time on it — the server may still reject it for other reasons.
    /// </summary>
    public static bool IsExpiredOrInvalid(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return true;

        try
        {
            // JWTs are three Base64Url segments separated by dots.
            var parts = token.Split('.');
            if (parts.Length != 3) return true;

            // Base64Url uses '-' and '_'; standard Base64 uses '+' and '/'.
            var b64 = parts[1].Replace('-', '+').Replace('_', '/');

            // Pad to a multiple of 4 (Base64 requirement).
            b64 += (b64.Length % 4) switch
            {
                2 => "==",
                3 => "=",
                _ => string.Empty   // 0 → no padding needed; 1 → impossible in valid Base64Url
            };

            var json = Encoding.UTF8.GetString(Convert.FromBase64String(b64));
            using var doc = JsonDocument.Parse(json);

            // Missing exp claim → treat as expired so we don't store a non-expiring token.
            if (!doc.RootElement.TryGetProperty("exp", out var expEl)) return true;

            var exp = expEl.GetInt64();
            var threshold = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + ClockSkewSeconds;
            return threshold >= exp;
        }
        catch
        {
            // Anything we can't parse is treated as expired.
            return true;
        }
    }

    public static string? TryGetEmail(string? token)
    {
        try
        {
            using var doc = DecodePayload(token);
            if (doc is null) return null;

            return doc.RootElement.TryGetProperty("sub", out var sub)
                ? sub.GetString()
                : null;
        }
        catch
        {
            return null;
        }
    }

    public static string? GetStoredEmail()
    {
        string? token = null;
        try { token = SecureStorage.GetAsync("auth_token").GetAwaiter().GetResult(); } catch { }
        if (string.IsNullOrEmpty(token))
            token = Preferences.Get("auth_token_fallback", null as string);

        return TryGetEmail(token);
    }

    public static async Task<string?> GetStoredEmailAsync()
    {
        string? token = null;
        try { token = await SecureStorage.GetAsync("auth_token"); } catch { }
        if (string.IsNullOrEmpty(token))
            token = Preferences.Get("auth_token_fallback", null as string);

        return TryGetEmail(token);
    }

    private static JsonDocument? DecodePayload(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        // JWTs are three Base64Url segments separated by dots.
        var parts = token.Split('.');
        if (parts.Length != 3) return null;

        // Base64Url uses '-' and '_'; standard Base64 uses '+' and '/'.
        var b64 = parts[1].Replace('-', '+').Replace('_', '/');

        // Pad to a multiple of 4 (Base64 requirement).
        b64 += (b64.Length % 4) switch
        {
            2 => "==",
            3 => "=",
            _ => string.Empty
        };

        var json = Encoding.UTF8.GetString(Convert.FromBase64String(b64));
        return JsonDocument.Parse(json);
    }
}
