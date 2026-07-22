using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Muninn.Models;
using Muninn.Services;
using System.Collections.ObjectModel;

namespace Muninn.ViewModels;

public partial class ScreenshotsReviewViewModel : BaseViewModel
{
    private readonly IScreenshotReviewService _screenshotReviewService;
    private readonly IApiService _apiService;

    private ObservableCollection<ScreenshotCandidate>? _trackedScreenshots;

    [ObservableProperty]
    private ObservableCollection<ScreenshotCandidate> _screenshots = [];

    [ObservableProperty]
    private int _pendingCount;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isSaving;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public string CountLabel => PendingCount == 1 ? "1 new" : $"{PendingCount} new";
    public bool HasScreenshots => PendingCount > 0;
    public bool ShowEmptyState => !IsLoading && !HasScreenshots && string.IsNullOrEmpty(ErrorMessage);

    public ScreenshotsReviewViewModel(
        IScreenshotReviewService screenshotReviewService,
        IApiService apiService)
    {
        _screenshotReviewService = screenshotReviewService;
        _apiService = apiService;
    }

    partial void OnScreenshotsChanged(ObservableCollection<ScreenshotCandidate> value)
    {
        if (_trackedScreenshots is not null)
            _trackedScreenshots.CollectionChanged -= OnScreenshotsCollectionChanged;

        _trackedScreenshots = value;
        _trackedScreenshots.CollectionChanged += OnScreenshotsCollectionChanged;

        RefreshCountState();
    }

    partial void OnIsLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowEmptyState));
    }

    partial void OnErrorMessageChanged(string value)
    {
        OnPropertyChanged(nameof(ShowEmptyState));
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            var items = await _screenshotReviewService.GetUnreviewedScreenshotsAsync();
            Screenshots = new ObservableCollection<ScreenshotCandidate>(items);
        }
        catch
        {
            ErrorMessage = "Couldn't load screenshots.";
            Screenshots = [];
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync(ScreenshotCandidate? screenshot)
    {
        if (screenshot is null || IsSaving) return;

        IsSaving = true;
        ErrorMessage = string.Empty;

        try
        {
            var image = await _screenshotReviewService.LoadImageDataAsync(screenshot.LocalIdentifier);
            var presign = await _apiService.GetPresignedUploadUrlAsync(image.ContentType);
            await _apiService.UploadBytesAsync(presign.UploadUrl, image.Bytes, image.ContentType);
            await _apiService.CreateImageSaveAsync(presign.Key);
            await MarkReviewedAndRemoveAsync(screenshot);
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch
        {
            ErrorMessage = "Couldn't save screenshot.";
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private async Task IgnoreAsync(ScreenshotCandidate? screenshot)
    {
        if (screenshot is null) return;

        try
        {
            await MarkReviewedAndRemoveAsync(screenshot);
        }
        catch
        {
            ErrorMessage = "Couldn't ignore screenshot.";
        }
    }

    private async Task MarkReviewedAndRemoveAsync(ScreenshotCandidate screenshot)
    {
        await _screenshotReviewService.MarkReviewedAsync(screenshot.LocalIdentifier);
        Screenshots.Remove(screenshot);
        RefreshCountState();
        WeakReferenceMessenger.Default.Send(new ScreenshotReviewCountChangedMessage(Screenshots.Count));
    }

    private void OnScreenshotsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        RefreshCountState();
    }

    private void RefreshCountState()
    {
        PendingCount = Screenshots.Count;
        OnPropertyChanged(nameof(CountLabel));
        OnPropertyChanged(nameof(HasScreenshots));
        OnPropertyChanged(nameof(ShowEmptyState));
    }
}
