using System.Text.Json;
using CommunityToolkit.Mvvm.Messaging;
using CoreGraphics;
using Foundation;
using Muninn.Models;
using Muninn.Services;
using Photos;
using UIKit;

namespace Muninn.Platforms.iOS.Services;

public class IosScreenshotReviewService : IScreenshotReviewService
{
    private const string BaselineTicksKey = "screenshot_review_baseline_ticks";
    private const string PrimingDeferredKey = "screenshot_review_priming_deferred";
    private static readonly TimeSpan CountCacheDuration = TimeSpan.FromSeconds(5);
    // Thumbnails are shown in ~full-width review cards. 1024px AspectFill is plenty
    // and decodes to ~4 MB; the old 1800pt × 3× scale = 5400px target decoded to
    // ~116 MB per image, a separate review-page memory hazard.
    private const int ThumbnailMaxPixels = 1024;
    private readonly SemaphoreSlim _storeLock = new(1, 1);
    private readonly SemaphoreSlim _countLock = new(1, 1);
    private DateTime _lastCountRefreshAt = DateTime.MinValue;
    private int? _cachedUnreviewedCount;

    public IosScreenshotReviewService()
    {
        // Badge freshness while the app is frontmost: taking a screenshot inside the
        // app fires no lifecycle event (no OnResume, no OnAppearing), so nothing
        // refreshed the unreviewed count and the badge never appeared. Observe the
        // system took-a-screenshot notification, wait briefly for Photos to index the
        // new asset into the Screenshots smart album, then publish a fresh count.
        // Screenshots taken while backgrounded are covered by App.OnResume.
        UIApplication.Notifications.ObserveUserDidTakeScreenshot(async (_, _) =>
        {
            try
            {
                Log("UserDidTakeScreenshot notification — refreshing count in 2 s");
                await Task.Delay(TimeSpan.FromSeconds(2));
                _cachedUnreviewedCount = null;
                _lastCountRefreshAt = DateTime.MinValue;
                var count = await GetUnreviewedCountAsync();
                // Task.Delay resumed us on a threadpool thread; the message updates
                // UI-bound view-model properties, so deliver it on the main thread
                // (the OnResume publish path is main-thread for the same reason).
                await MainThread.InvokeOnMainThreadAsync(() =>
                    WeakReferenceMessenger.Default.Send(new ScreenshotReviewCountChangedMessage(count)));
                Log($"UserDidTakeScreenshot → published count {count} (main thread)");
            }
            catch { /* badge freshness is best-effort */ }
        });
    }

    // Diagnostic breadcrumb. Streams to the device console so a repro can show exactly
    // which photo-path method ran, how many assets it touched, and at what target size,
    // and which build it was (see BUILD TAG in MauiProgram).
    private static void Log(string message)
    {
        Console.WriteLine($"[SSDIAG] {message}");
        System.Diagnostics.Debug.WriteLine($"[SSDIAG] {message}");
    }

    private string ReviewedStorePath =>
        Path.Combine(FileSystem.AppDataDirectory, "reviewed-screenshots.json");

    public Task<ScreenshotPhotoAuthorizationState> GetAuthorizationStateAsync()
    {
        var status = PHPhotoLibrary.GetAuthorizationStatus(PHAccessLevel.ReadWrite);
        return Task.FromResult(MapStatus(status));
    }

    public async Task<ScreenshotPhotoAuthorizationState> RequestFullAccessAsync()
    {
        Log("RequestFullAccessAsync ENTER (grant handler)");
        var status = await PHPhotoLibrary.RequestAuthorizationAsync(PHAccessLevel.ReadWrite);
        var state = MapStatus(status);
        Log($"RequestFullAccessAsync authorization result = {state}");

        if (state == ScreenshotPhotoAuthorizationState.Authorized)
        {
            Preferences.Set(PrimingDeferredKey, false);
            await CaptureBaselineIfNeededAsync();
        }

        Log("RequestFullAccessAsync EXIT");
        return state;
    }

    public async Task<bool> ShouldShowPrimingAutomaticallyAsync()
    {
        var state = await GetAuthorizationStateAsync();
        return state == ScreenshotPhotoAuthorizationState.NotDetermined &&
               !Preferences.Get(PrimingDeferredKey, false) &&
               !HasBaseline();
    }

    public Task CaptureBaselineIfNeededAsync()
    {
        Log("CaptureBaselineIfNeededAsync ENTER");
        if (HasBaseline())
        {
            Log("CaptureBaselineIfNeededAsync EXIT (baseline already set, no fetch)");
            return Task.CompletedTask;
        }

        // Baseline is simply "now": only screenshots taken AFTER access was granted
        // should ever surface, and UtcNow gives exactly that semantics. We deliberately
        // do NOT read the newest existing screenshot here anymore — that required a
        // Photos fetch on the authorization-grant callback. The iOS 27 physical-device
        // respring investigation needs the grant path to touch Photos as little as
        // possible, so this removes the last Photos enumeration from the grant callback.
        // Existing screenshots (all older than now) stay excluded; a screenshot taken
        // after this instant has creationDate > baseline and becomes a candidate.
        var baseline = DateTime.UtcNow;
        Preferences.Set(BaselineTicksKey, baseline.Ticks);
        _cachedUnreviewedCount = null;
        _lastCountRefreshAt = DateTime.MinValue;
        Log($"CaptureBaselineIfNeededAsync EXIT (baseline set to {baseline:u}, no Photos fetch)");
        return Task.CompletedTask;
    }

    public Task DeferPrimingAsync()
    {
        Preferences.Set(PrimingDeferredKey, true);
        return Task.CompletedTask;
    }

    public async Task<int> GetUnreviewedCountAsync()
    {
        var now = DateTime.UtcNow;
        if (_cachedUnreviewedCount is int cached &&
            now - _lastCountRefreshAt < CountCacheDuration)
        {
            Log($"GetUnreviewedCountAsync EXIT = {cached} (cached, no Photos fetch)");
            return cached;
        }

        await _countLock.WaitAsync();
        try
        {
            now = DateTime.UtcNow;
            if (_cachedUnreviewedCount is int cachedAfterLock &&
                now - _lastCountRefreshAt < CountCacheDuration)
            {
                Log($"GetUnreviewedCountAsync EXIT = {cachedAfterLock} (cached after wait, no Photos fetch)");
                return cachedAfterLock;
            }

            var count = await ComputeUnreviewedCountAsync();
            _cachedUnreviewedCount = count;
            _lastCountRefreshAt = DateTime.UtcNow;
            return count;
        }
        finally
        {
            _countLock.Release();
        }
    }

    private async Task<int> ComputeUnreviewedCountAsync()
    {
        // Counting must be cheap: scope to screenshots-since-baseline and use the
        // fetch result's Count. Never enumerate the whole library, never decode images.
        Log("GetUnreviewedCountAsync ENTER (count only, no image decode)");
        var baseline = GetBaseline();
        if (baseline is null)
        {
            Log("GetUnreviewedCountAsync EXIT = 0 (no baseline, no fetch)");
            return 0;
        }

        var result = FetchScreenshotsResult(baseline);
        try
        {
            if (result is null || result.Count == 0)
            {
                Log("GetUnreviewedCountAsync EXIT = 0 (no screenshots since baseline)");
                return 0;
            }

            var reviewed = await LoadReviewedAsync();
            // Nothing reviewed yet → the count is just the fetch count (O(1), no PHAsset
            // materialization). This is the common case on a fresh install.
            if (reviewed.Count == 0)
            {
                Log($"GetUnreviewedCountAsync EXIT = {result.Count} (PHFetchResult.Count, O(1))");
                return (int)result.Count;
            }

            // Otherwise enumerate the (small, baseline-scoped) set, reading only the
            // local identifier — still no image decoding.
            var count = 0;
            for (nint i = 0; i < result.Count; i++)
            {
                if (result.ObjectAt(i) is PHAsset asset && !reviewed.Contains(asset.LocalIdentifier))
                    count++;
            }

            Log($"GetUnreviewedCountAsync EXIT = {count} (enumerated {result.Count} ids, no decode)");
            return count;
        }
        finally
        {
            result?.Dispose();
        }
    }

    public async Task<IReadOnlyList<ScreenshotCandidate>> GetUnreviewedScreenshotsAsync()
    {
        Log("GetUnreviewedScreenshotsAsync ENTER (review page; batch thumbnail load)");
        var reviewed = await LoadReviewedAsync();
        var assets = FetchUnreviewedAssets(reviewed);
        Log($"GetUnreviewedScreenshotsAsync will load {assets.Count} thumbnails at {ThumbnailMaxPixels}px");
        var candidates = new List<ScreenshotCandidate>(assets.Count);

        foreach (var asset in assets)
        {
            var thumbnail = await LoadThumbnailAsync(asset);
            candidates.Add(new ScreenshotCandidate
            {
                LocalIdentifier = asset.LocalIdentifier,
                CreatedAt = ToDateTime(asset.CreationDate) ?? DateTime.UtcNow,
                ThumbnailBytes = thumbnail
            });
        }

        Log($"GetUnreviewedScreenshotsAsync EXIT ({candidates.Count} candidates)");
        return candidates;
    }

    public Task<ScreenshotImageData> LoadImageDataAsync(string localIdentifier)
    {
        Log($"LoadImageDataAsync ENTER (FULL image data, single asset {localIdentifier})");
        var asset = FetchAsset(localIdentifier)
            ?? throw new InvalidOperationException("Screenshot no longer exists in Photos.");

        var tcs = new TaskCompletionSource<ScreenshotImageData>();
        var options = new PHImageRequestOptions
        {
            NetworkAccessAllowed = true,
            Synchronous = false,
            DeliveryMode = PHImageRequestOptionsDeliveryMode.HighQualityFormat
        };

        PHImageManager.DefaultManager.RequestImageDataAndOrientation(
            asset,
            options,
            (data, dataUti, orientation, info) =>
            {
                if (data is null)
                {
                    tcs.TrySetException(new InvalidOperationException("Couldn't load screenshot image data."));
                    return;
                }

                var contentType = ContentTypeFor(dataUti);
                tcs.TrySetResult(new ScreenshotImageData(data.ToArray(), contentType));
            });

        return tcs.Task;
    }

    public async Task MarkReviewedAsync(string localIdentifier)
    {
        await _storeLock.WaitAsync();
        try
        {
            var reviewed = await LoadReviewedUnlockedAsync();
            reviewed.Add(localIdentifier);
            var json = JsonSerializer.Serialize(reviewed.OrderBy(id => id));
            Directory.CreateDirectory(FileSystem.AppDataDirectory);
            await File.WriteAllTextAsync(ReviewedStorePath, json);
            _cachedUnreviewedCount = null;
            _lastCountRefreshAt = DateTime.MinValue;
        }
        finally
        {
            _storeLock.Release();
        }
    }

    private List<PHAsset> FetchUnreviewedAssets(HashSet<string> reviewed)
    {
        var baseline = GetBaseline();
        if (baseline is null) return [];

        var result = FetchScreenshotsResult(baseline);
        try
        {
            Log($"FetchUnreviewedAssets fetch count = {(result?.Count.ToString() ?? "null")} (screenshots since baseline)");
            if (result is null || result.Count == 0) return [];

            // The fetch is already screenshots-only, baseline-bounded, and sorted
            // creationDate-descending, so we just drop reviewed ones in order.
            var assets = new List<PHAsset>((int)result.Count);
            for (nint i = 0; i < result.Count; i++)
            {
                if (result.ObjectAt(i) is PHAsset asset && !reviewed.Contains(asset.LocalIdentifier))
                    assets.Add(asset);
            }

            return assets;
        }
        finally
        {
            result?.Dispose();
        }
    }

    /// <summary>
    /// Fetches ONLY screenshots, from the Screenshots smart album, optionally bounded
    /// to creationDate &gt; <paramref name="after"/> (the access-granted baseline), sorted
    /// newest-first. Returns null when the device has no Screenshots smart album.
    ///
    /// There is deliberately NO fallback to the general image library. The previous
    /// fallback materialised every image matching the fetch options when the screenshot
    /// query came back empty; during an unbounded baseline fetch that could mean the
    /// entire library. If there are no screenshots since the baseline, the correct answer
    /// is "nothing to review" — not "scan every image to prove it".
    /// </summary>
    private static PHFetchResult? FetchScreenshotsResult(DateTime? after)
    {
        var options = new PHFetchOptions
        {
            SortDescriptors =
            [
                new NSSortDescriptor("creationDate", false)
            ]
        };

        // Filter by the asset's screenshot media subtype instead of the Screenshots
        // smart album. Album membership is indexed lazily by Photos — a brand-new
        // screenshot can take longer than our refresh delay to appear in the album,
        // so counts lagged (badge showed stale values). mediaSubtypes is intrinsic
        // to the asset from the moment it exists. The predicate is evaluated by
        // Photos itself and the fetch result stays lazy — this is NOT the removed
        // "fetch all images and filter in-app" hazard.
        var screenshotBit = (uint)PHAssetMediaSubtype.Screenshot;
        options.Predicate = after is not null
            ? NSPredicate.FromFormat(
                $"(mediaSubtypes & {screenshotBit}) != 0 AND creationDate > %@", ToNSDate(after.Value))
            : NSPredicate.FromFormat($"(mediaSubtypes & {screenshotBit}) != 0");

        var result = PHAsset.FetchAssets(PHAssetMediaType.Image, options);
        Log($"FetchScreenshotsResult: subtype-predicate fetch, after={(after?.ToString("u") ?? "null")}, count={result.Count} (lazy, no decode)");
        return result;
    }

    private static PHAsset? FetchAsset(string localIdentifier)
    {
        var result = PHAsset.FetchAssetsUsingLocalIdentifiers([localIdentifier], null);
        return result.Count > 0 ? result.ObjectAt(0) as PHAsset : null;
    }

    private static Task<byte[]> LoadThumbnailAsync(PHAsset asset)
    {
        var tcs = new TaskCompletionSource<byte[]>();
        // Bounded display-size target (not full resolution). AspectFill at 1024px is
        // ample for a review card and keeps each decode to a few MB.
        var targetSize = new CGSize(ThumbnailMaxPixels, ThumbnailMaxPixels);
        Log($"LoadThumbnailAsync requesting target {ThumbnailMaxPixels}x{ThumbnailMaxPixels} (AspectFill, Fast)");
        var options = new PHImageRequestOptions
        {
            NetworkAccessAllowed = true,
            Synchronous = false,
            ResizeMode = PHImageRequestOptionsResizeMode.Fast,
            DeliveryMode = PHImageRequestOptionsDeliveryMode.HighQualityFormat
        };

        PHImageManager.DefaultManager.RequestImageForAsset(
            asset,
            targetSize,
            PHImageContentMode.AspectFill,
            options,
            (image, info) =>
            {
                if (image is null)
                {
                    tcs.TrySetResult([]);
                    return;
                }

                // JPEG, not PNG: a PNG of a photo-sized bitmap is many times larger and
                // there's no transparency to preserve in a screenshot thumbnail.
                var data = image.AsJPEG(0.9f) ?? image.AsPNG();
                tcs.TrySetResult(data?.ToArray() ?? []);
            });

        return tcs.Task;
    }

    private async Task<HashSet<string>> LoadReviewedAsync()
    {
        await _storeLock.WaitAsync();
        try
        {
            return await LoadReviewedUnlockedAsync();
        }
        finally
        {
            _storeLock.Release();
        }
    }

    private async Task<HashSet<string>> LoadReviewedUnlockedAsync()
    {
        if (!File.Exists(ReviewedStorePath)) return [];

        try
        {
            var json = await File.ReadAllTextAsync(ReviewedStorePath);
            return JsonSerializer.Deserialize<HashSet<string>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static ScreenshotPhotoAuthorizationState MapStatus(PHAuthorizationStatus status) =>
        status switch
        {
            PHAuthorizationStatus.Authorized => ScreenshotPhotoAuthorizationState.Authorized,
            PHAuthorizationStatus.Limited => ScreenshotPhotoAuthorizationState.Limited,
            PHAuthorizationStatus.Denied => ScreenshotPhotoAuthorizationState.Denied,
            PHAuthorizationStatus.Restricted => ScreenshotPhotoAuthorizationState.Restricted,
            _ => ScreenshotPhotoAuthorizationState.NotDetermined
        };

    private static string ContentTypeFor(string? dataUti)
    {
        if (string.IsNullOrWhiteSpace(dataUti)) return "image/png";

        var value = dataUti.ToLowerInvariant();
        if (value.Contains("jpeg") || value.Contains("jpg")) return "image/jpeg";
        if (value.Contains("heic")) return "image/heic";
        if (value.Contains("webp")) return "image/webp";
        return "image/png";
    }

    private static DateTime? ToDateTime(NSDate? date)
    {
        if (date is null) return null;
        return DateTime.UnixEpoch.AddSeconds(date.SecondsSince1970).ToUniversalTime();
    }

    private static NSDate ToNSDate(DateTime date)
    {
        var utc = date.ToUniversalTime();
        return NSDate.FromTimeIntervalSince1970((utc - DateTime.UnixEpoch).TotalSeconds);
    }

    private static DateTime? GetBaseline()
    {
        var ticks = Preferences.Get(BaselineTicksKey, 0L);
        return ticks <= 0 ? null : new DateTime(ticks, DateTimeKind.Utc);
    }

    private static bool HasBaseline() => Preferences.Get(BaselineTicksKey, 0L) > 0;
}
