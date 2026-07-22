using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Muninn.Models;
using Muninn.Views;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Muninn.Services;

/// <summary>
/// Wraps all HTTP calls to the Muninn Spring Boot backend.
/// Base URL is resolved from appsettings.json plus optional debug overrides.
/// - Simulator / Mac-local runs use ApiBaseUrlLocal (127.0.0.1)
/// - Physical iPhone runs use ApiBaseUrlLan (the private Tailscale Serve URL in development)
/// - Debug builds can override via:
///   1) DebugBaseUrlOverride below
///   2) appsettings.local.json (AppDataDirectory or app bundle)
///   3) MUNINN_API_BASE_URL environment variable
///   4) Preferences["debug_api_base_url_override"]
/// Throws ApiException on non-2xx responses.
///
/// Mid-session 401 handling: if an authenticated request (one where we have already
/// set the Bearer token) returns 401, the server has rejected our token — it has either
/// expired or been invalidated.  We clear the stored token and navigate back to AuthPage
/// so the user can sign in again, rather than silently showing an empty screen.
/// </summary>
public class ApiService : IApiService
{
    // Fastest manual override while actively iterating. Leave null for normal resolution.
#if DEBUG
    private const string? DebugBaseUrlOverride = null;
#endif

    private readonly HttpClient _httpClient;
    private readonly IServiceProvider _services;

    // Interlocked flag: 0 = idle, 1 = already handling a mid-session 401.
    // Prevents multiple concurrent 401 responses from all triggering navigation.
    private int _handlingUnauthorized;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ApiService(IConfiguration configuration, IServiceProvider services)
    {
        var baseUrl = ResolveBaseUrl(configuration);
        Console.WriteLine($"[Muninn] API Base URL: {baseUrl}");
        Debug.WriteLine($"[Muninn] API Base URL: {baseUrl}");
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUrl),
            // 30-second ceiling prevents hangs on Render cold-starts (which can take
            // 30-60 s) from accumulating open connections and buffers in memory.
            Timeout = TimeSpan.FromSeconds(30),
        };
        _services = services;
    }

    private static string ResolveBaseUrl(IConfiguration configuration)
    {
        var debugOverride = ResolveDebugOverride(configuration);
        if (!string.IsNullOrWhiteSpace(debugOverride))
            return NormalizeBaseUrl(debugOverride);

        var localBaseUrl = configuration["ApiBaseUrlLocal"] ?? "http://127.0.0.1:8080";
        var lanBaseUrl = configuration["ApiBaseUrlLan"] ?? localBaseUrl;

        var resolvedBaseUrl = DeviceInfo.Platform == DevicePlatform.iOS && DeviceInfo.DeviceType == DeviceType.Physical
            ? lanBaseUrl
            : localBaseUrl;

        return NormalizeBaseUrl(resolvedBaseUrl);
    }

    private static string? ResolveDebugOverride(IConfiguration configuration)
    {
#if DEBUG
        if (!string.IsNullOrWhiteSpace(DebugBaseUrlOverride))
            return DebugBaseUrlOverride;

        var configOverride = configuration["ApiBaseUrlOverride"];
        if (!string.IsNullOrWhiteSpace(configOverride))
            return configOverride;

        var environmentOverride = Environment.GetEnvironmentVariable("MUNINN_API_BASE_URL");
        if (!string.IsNullOrWhiteSpace(environmentOverride))
            return environmentOverride;

        var preferenceOverride = Preferences.Get("debug_api_base_url_override", string.Empty);
        if (!string.IsNullOrWhiteSpace(preferenceOverride))
            return preferenceOverride;
#endif
        return null;
    }

    private static string NormalizeBaseUrl(string baseUrl)
    {
        return baseUrl.EndsWith("/", StringComparison.Ordinal)
            ? baseUrl.TrimEnd('/')
            : baseUrl;
    }

    // ── Auth token management ─────────────────────────────────────────────────

    public void SetAuthToken(string token)
    {
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Reset the logout guard so a future expiry can trigger the flow again.
        Interlocked.Exchange(ref _handlingUnauthorized, 0);
    }

    public void ClearAuthToken()
    {
        _httpClient.DefaultRequestHeaders.Authorization = null;
    }

    // ── Endpoints ─────────────────────────────────────────────────────────────

    public async Task<string> RegisterAsync(string email, string password)
    {
        using var response = await _httpClient.PostAsJsonAsync("/auth/register", new { email, password });
        await EnsureSuccessAsync(response, "Registration failed.");
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        return body!.Token;
    }

    public async Task<string> LoginAsync(string email, string password)
    {
        using var response = await _httpClient.PostAsJsonAsync("/auth/login", new { email, password });
        await EnsureSuccessAsync(response, "Incorrect email or password.");
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        return body!.Token;
    }

    public async Task<AccountResponse> GetAccountAsync()
    {
        using var response = await _httpClient.GetAsync("/account");
        await EnsureSuccessAsync(response, "Failed to load account.");
        return (await response.Content.ReadFromJsonAsync<AccountResponse>(JsonOptions))!;
    }

    public async Task<AccountResponse> UpdateAccountAsync(AccountUpdateRequest request)
    {
        using var response = await _httpClient.PutAsJsonAsync("/account", request);
        await EnsureSuccessAsync(response, "Failed to update account.");
        return (await response.Content.ReadFromJsonAsync<AccountResponse>(JsonOptions))!;
    }

    public async Task ChangePasswordAsync(string currentPassword, string newPassword)
    {
        using var response = await _httpClient.PostAsJsonAsync("/account/password", new { currentPassword, newPassword });
        await EnsureSuccessAsync(response, "Failed to change password.");
    }

    public async Task DeleteAccountAsync()
    {
        using var response = await _httpClient.DeleteAsync("/account");
        await EnsureSuccessAsync(response, "Failed to delete account.");
    }

    public async Task<List<SaveModel>> GetSavesAsync(string? status = null)
    {
        var url = status is not null ? $"/saves?status={status}" : "/saves";
        using var response = await _httpClient.GetAsync(url);
        await EnsureSuccessAsync(response, "Failed to load saves.");
        return await response.Content.ReadFromJsonAsync<List<SaveModel>>(JsonOptions) ?? [];
    }

    public async Task<SaveModel> GetSaveByIdAsync(long id)
    {
        using var response = await _httpClient.GetAsync($"/saves/{id}");
        await EnsureSuccessAsync(response, "Save not found.");
        return (await response.Content.ReadFromJsonAsync<SaveModel>(JsonOptions))!;
    }

    public async Task<SaveModel> CreateSaveAsync(string type, string? sourceUrl)
    {
        using var response = await _httpClient.PostAsJsonAsync("/saves", new { type, sourceUrl });
        await EnsureSuccessAsync(response, "Failed to save link.");
        return (await response.Content.ReadFromJsonAsync<SaveModel>(JsonOptions))!;
    }

    public async Task CompleteSaveAsync(long id)
    {
        using var response = await _httpClient.PostAsync($"/saves/{id}/complete", null);
        await EnsureSuccessAsync(response, "Failed to complete save.");
    }

    public async Task UncompleteSaveAsync(long id)
    {
        using var response = await _httpClient.PostAsync($"/saves/{id}/uncomplete", null);
        await EnsureSuccessAsync(response, "Failed to move save back to inbox.");
    }

    public async Task ArchiveSaveAsync(long id)
    {
        using var response = await _httpClient.PostAsync($"/saves/{id}/archive", null);
        await EnsureSuccessAsync(response, "Failed to archive save.");
    }

    public async Task UnarchiveSaveAsync(long id)
    {
        using var response = await _httpClient.PostAsync($"/saves/{id}/unarchive", null);
        await EnsureSuccessAsync(response, "Failed to move save back to inbox.");
    }

    public async Task DeleteSaveAsync(long id)
    {
        using var response = await _httpClient.DeleteAsync($"/saves/{id}");
        await EnsureSuccessAsync(response, "Failed to delete save.");
    }

    public async Task<SaveModel> UpdateSaveCategoryAsync(long id, string category)
    {
        using var response = await _httpClient.PutAsJsonAsync($"/saves/{id}/category", new { category });
        await EnsureSuccessAsync(response, "Failed to update category.");
        return (await response.Content.ReadFromJsonAsync<SaveModel>(JsonOptions))!;
    }

    public async Task DeleteCategoryAsync(string category)
    {
        using var response = await _httpClient.DeleteAsync($"/saves/categories?category={Uri.EscapeDataString(category)}");
        await EnsureSuccessAsync(response, "Failed to delete category.");
    }

    public async Task<List<CategoryModel>> GetCategoriesAsync()
    {
        using var response = await _httpClient.GetAsync("/categories");
        await EnsureSuccessAsync(response, "Failed to load categories.");
        return (await response.Content.ReadFromJsonAsync<List<CategoryModel>>(JsonOptions)) ?? [];
    }

    public async Task<CategoryModel> CreateCategoryAsync(string name)
    {
        using var response = await _httpClient.PostAsJsonAsync("/categories", new { name });
        await EnsureSuccessAsync(response, "Failed to add category.");
        return (await response.Content.ReadFromJsonAsync<CategoryModel>(JsonOptions))!;
    }

    public async Task<CategoryModel> RenameCategoryAsync(long id, string name)
    {
        using var response = await _httpClient.PutAsJsonAsync($"/categories/{id}", new { name });
        await EnsureSuccessAsync(response, "Failed to rename category.");
        return (await response.Content.ReadFromJsonAsync<CategoryModel>(JsonOptions))!;
    }

    public async Task DeleteCategoryByIdAsync(long id)
    {
        using var response = await _httpClient.DeleteAsync($"/categories/{id}");
        await EnsureSuccessAsync(response, "Failed to delete category.");
    }

    public async Task<PresignResponse> GetPresignedUploadUrlAsync(string contentType = "image/png")
    {
        using var response = await _httpClient.GetAsync($"/uploads/presign?contentType={Uri.EscapeDataString(contentType)}");
        await EnsureSuccessAsync(response, "Failed to get upload URL.");
        return (await response.Content.ReadFromJsonAsync<PresignResponse>(JsonOptions))!;
    }

    public async Task UploadBytesAsync(string presignedUrl, byte[] bytes, string contentType)
    {
        // The presigned URL points directly at R2, not the backend — must use a fresh
        // HttpClient without the backend BaseAddress or Authorization header.
        using var r2Client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        using var response = await r2Client.PutAsync(presignedUrl, content);
        if (!response.IsSuccessStatusCode)
            throw new ApiException((int)response.StatusCode, "Failed to upload image to storage.");
    }

    public async Task<SaveModel> CreateImageSaveAsync(string imageRef)
    {
        using var response = await _httpClient.PostAsJsonAsync("/saves", new { type = "image", imageRef });
        await EnsureSuccessAsync(response, "Failed to create image save.");
        return (await response.Content.ReadFromJsonAsync<SaveModel>(JsonOptions))!;
    }

    public async Task<bool> ProcessPendingShareAsync()
    {
        // The App Group suite name must match the value in both Entitlements.plist files.
        // Preferences.Get with a sharedName maps to NSUserDefaults(suiteName:) on iOS.
        // On Android/Mac it reads from a named SharedPreferences file — the key is never
        // written there, so this returns "" and exits immediately on non-iOS targets.
        const string groupId = "group.com.claycasani.muninn";
        const string key     = "pendingShareUrl";

        var url = Preferences.Get(key, string.Empty, groupId);
        if (string.IsNullOrEmpty(url)) return false;

        // Create the save.  Don't clear the key until this succeeds — if the network is
        // down or the request returns an error, the exception propagates to the caller and
        // the key stays set so the next session retries automatically.
        await CreateSaveAsync("link", url);

        // Only reached on success.
        Preferences.Remove(key, groupId);
        return true;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Throws <see cref="ApiException"/> for any non-2xx response.
    ///
    /// For 401 responses, behaviour depends on whether we have an auth token set:
    ///   • No token (login/register): throw "Incorrect email or password."
    ///   • Token set (mid-session): clear stored token, navigate to AuthPage, then throw
    ///     "Your session has expired." The ViewModel will catch this, but by the time it
    ///     updates ErrorMessage the user is already looking at the auth screen.
    /// </summary>
    private async Task EnsureSuccessAsync(HttpResponseMessage response, string defaultMessage)
    {
        if (response.IsSuccessStatusCode) return;

        var statusCode = (int)response.StatusCode;

        // ── Mid-session token expiry ─────────────────────────────────────────
        // If we have an Authorization header and the server returned 401, our token is
        // no longer valid.  Trigger the logout flow; the login/register 401 path below
        // is never reached in this branch.
        if (statusCode == 401 && _httpClient.DefaultRequestHeaders.Authorization is not null)
        {
            await HandleUnauthorizedAsync();
            throw new ApiException(401, "Your session has expired. Please sign in again.");
        }

        // ── Structured error body (from Spring's GlobalExceptionHandler) ─────
        // Covers 400 validation errors and the deliberate 404/409 status reasons
        // (e.g. category "already exists" / "need at least one category").
        if (statusCode is 400 or 404 or 409)
        {
            try
            {
                var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
                var msg = body?.Error;
                if (!string.IsNullOrWhiteSpace(msg))
                    throw new ApiException(statusCode, msg);
            }
            catch (ApiException) { throw; }
            catch { /* fall through to default message */ }
        }

        var message = statusCode switch
        {
            401 => "Incorrect email or password.",   // login/register path (no auth header)
            409 => "An account with that email already exists.",
            403 => "The current password is incorrect.",
            _   => defaultMessage
        };

        throw new ApiException(statusCode, message);
    }

    /// <summary>
    /// Clears the stored JWT and presents <see cref="AuthPage"/> as a modal.
    /// Protected by an Interlocked flag so that multiple concurrent 401 responses
    /// (e.g. Inbox and Archive both loading when the token expires) only trigger one
    /// navigation.
    ///
    /// Why modal and not GoToAsync?  GoToAsync("AuthPage") is a Shell relative-route push
    /// that can be silently blocked while a tab page's OnAppearing async chain is running.
    /// PushModalAsync is handled by UIKit's modal stack, independent of Shell's navigation
    /// graph, and works reliably from any call site.
    /// </summary>
    private Task HandleUnauthorizedAsync()
    {
        // Only the first caller proceeds; subsequent ones return immediately.
        if (Interlocked.CompareExchange(ref _handlingUnauthorized, 1, 0) != 0) return Task.CompletedTask;

        // Wipe the stored token from memory and persistent storage.
        ClearAuthToken();
        App.ClearStoredToken();

        // Resolve AuthPage from the injected DI container and present it modally.
        // Using the constructor-injected IServiceProvider is more reliable than the
        // Application.Current?.Handler?.MauiContext?.Services chain, which can be null
        // at runtime once the app is past its initial startup.
        Application.Current?.Dispatcher.Dispatch(async () =>
        {
            var authPage = _services.GetService<AuthPage>();
            if (authPage is not null)
                await Shell.Current.Navigation.PushModalAsync(authPage, animated: false);
        });

        return Task.CompletedTask;
    }

    // ── Private DTOs ─────────────────────────────────────────────────────────

    private record ErrorResponse([property: JsonPropertyName("error")] string? Error);
    private record AuthResponse([property: JsonPropertyName("token")] string Token);
}
