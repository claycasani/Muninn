using Muninn.Models;

namespace Muninn.Services;

public class ScreenshotReviewService : IScreenshotReviewService
{
    public Task<ScreenshotPhotoAuthorizationState> GetAuthorizationStateAsync() =>
        Task.FromResult(ScreenshotPhotoAuthorizationState.Unsupported);

    public Task<ScreenshotPhotoAuthorizationState> RequestFullAccessAsync() =>
        Task.FromResult(ScreenshotPhotoAuthorizationState.Unsupported);

    public Task<bool> ShouldShowPrimingAutomaticallyAsync() =>
        Task.FromResult(false);

    public Task CaptureBaselineIfNeededAsync() =>
        Task.CompletedTask;

    public Task DeferPrimingAsync() =>
        Task.CompletedTask;

    public Task<int> GetUnreviewedCountAsync() =>
        Task.FromResult(0);

    public Task<IReadOnlyList<ScreenshotCandidate>> GetUnreviewedScreenshotsAsync() =>
        Task.FromResult<IReadOnlyList<ScreenshotCandidate>>([]);

    public Task<ScreenshotImageData> LoadImageDataAsync(string localIdentifier) =>
        throw new NotSupportedException("Screenshot review is only available on iOS.");

    public Task MarkReviewedAsync(string localIdentifier) =>
        Task.CompletedTask;
}

