using Muninn.Models;

namespace Muninn.Services;

public enum ScreenshotPhotoAuthorizationState
{
    NotDetermined,
    Authorized,
    Limited,
    Denied,
    Restricted,
    Unsupported
}

public interface IScreenshotReviewService
{
    Task<ScreenshotPhotoAuthorizationState> GetAuthorizationStateAsync();
    Task<ScreenshotPhotoAuthorizationState> RequestFullAccessAsync();
    Task<bool> ShouldShowPrimingAutomaticallyAsync();
    Task CaptureBaselineIfNeededAsync();
    Task DeferPrimingAsync();
    Task<int> GetUnreviewedCountAsync();
    Task<IReadOnlyList<ScreenshotCandidate>> GetUnreviewedScreenshotsAsync();
    Task<ScreenshotImageData> LoadImageDataAsync(string localIdentifier);
    Task MarkReviewedAsync(string localIdentifier);
}

