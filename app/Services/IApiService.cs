using System.Text.Json.Serialization;
using Muninn.Models;

namespace Muninn.Services;

/// <summary>Response from GET /uploads/presign — key to store as imageRef, uploadUrl to PUT bytes to.</summary>
public record PresignResponse(
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("uploadUrl")] string UploadUrl
);

public record AccountResponse(
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("displayName")] string DisplayName,
    [property: JsonPropertyName("dailyDigestEnabled")] bool DailyDigestEnabled,
    [property: JsonPropertyName("digestTime")] string DigestTime,
    [property: JsonPropertyName("notificationsEnabled")] bool NotificationsEnabled,
    [property: JsonPropertyName("autoArchiveDays")] int AutoArchiveDays
);

public record AccountUpdateRequest(
    [property: JsonPropertyName("displayName")] string? DisplayName = null,
    [property: JsonPropertyName("dailyDigestEnabled")] bool? DailyDigestEnabled = null,
    [property: JsonPropertyName("digestTime")] string? DigestTime = null,
    [property: JsonPropertyName("notificationsEnabled")] bool? NotificationsEnabled = null,
    [property: JsonPropertyName("autoArchiveDays")] int? AutoArchiveDays = null
);

public interface IApiService
{
    void SetAuthToken(string token);

    /// <summary>
    /// Removes the Bearer token from the HTTP client so subsequent requests are
    /// unauthenticated.  Call this alongside <see cref="App.ClearStoredToken"/> when
    /// signing the user out.
    /// </summary>
    void ClearAuthToken();
    Task<string> RegisterAsync(string email, string password);
    Task<string> LoginAsync(string email, string password);
    Task<AccountResponse> GetAccountAsync();
    Task<AccountResponse> UpdateAccountAsync(AccountUpdateRequest request);
    Task ChangePasswordAsync(string currentPassword, string newPassword);
    Task DeleteAccountAsync();
    Task<List<SaveModel>> GetSavesAsync(string? status = null);
    Task<SaveModel> GetSaveByIdAsync(long id);
    Task<SaveModel> CreateSaveAsync(string type, string? sourceUrl);
    Task CompleteSaveAsync(long id);
    Task UncompleteSaveAsync(long id);
    Task ArchiveSaveAsync(long id);
    Task UnarchiveSaveAsync(long id);
    Task DeleteSaveAsync(long id);
    Task<SaveModel> UpdateSaveCategoryAsync(long id, string category);
    Task DeleteCategoryAsync(string category);

    // User-editable category list.
    Task<List<CategoryModel>> GetCategoriesAsync();
    Task<CategoryModel> CreateCategoryAsync(string name);
    Task<CategoryModel> RenameCategoryAsync(long id, string name);
    Task DeleteCategoryByIdAsync(long id);

    /// <summary>Asks the backend for a presigned PUT URL + key for a direct R2 upload.</summary>
    Task<PresignResponse> GetPresignedUploadUrlAsync(string contentType = "image/png");

    /// <summary>PUTs raw image bytes directly to the R2 presigned URL (bypasses the backend).</summary>
    Task UploadBytesAsync(string presignedUrl, byte[] bytes, string contentType);

    /// <summary>Creates an image save record after the bytes have been uploaded to R2.</summary>
    Task<SaveModel> CreateImageSaveAsync(string imageRef);

    /// <summary>
    /// Checks the iOS Share Extension's App Group defaults for a pending URL and, if one
    /// is found, creates a save via <see cref="CreateSaveAsync"/>.
    ///
    /// The pending key is cleared <em>only on success</em>, so a failed network call leaves
    /// the URL in place and the next call retries automatically.
    ///
    /// Returns <c>true</c> if a share was processed, <c>false</c> if nothing was pending.
    /// On non-iOS platforms the shared preference suite is never written, so this always
    /// returns <c>false</c> harmlessly.
    /// </summary>
    Task<bool> ProcessPendingShareAsync();
}
