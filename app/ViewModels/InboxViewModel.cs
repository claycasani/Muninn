using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.Graphics;
using Muninn.Models;
using Muninn.Services;
using System.Collections.ObjectModel;
using Muninn.Views;
using System.Threading;

namespace Muninn.ViewModels;

public partial class InboxViewModel : BaseViewModel
{
    private const int SearchDebounceDelayMs = 200;

    private readonly IApiService _apiService;
    private readonly IScreenshotReviewService _screenshotReviewService;
    private List<SaveModel> _allSaves = [];
    private CancellationTokenSource? _searchDebounceCts;

    [ObservableProperty]
    private ObservableCollection<SaveGroup> _saveGroups = [];

    [ObservableProperty]
    private ObservableCollection<CategoryFilterOption> _categoryFilters = [];

    [ObservableProperty]
    private string _selectedCategory = "All";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _linkInput = string.Empty;

    [ObservableProperty]
    private bool _isLinkBarVisible;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isUploadingImage;

    [ObservableProperty]
    private int _unreviewedScreenshotCount;

    public bool HasUnreviewedScreenshots => UnreviewedScreenshotCount > 0;
    public string UnreviewedScreenshotCountText => UnreviewedScreenshotCount > 99 ? "99+" : UnreviewedScreenshotCount.ToString();
    public bool HasActiveSearchOrFilter => !string.IsNullOrWhiteSpace(SearchQuery) || SelectedCategory != "All";
    public string EmptyTitle => HasActiveSearchOrFilter ? "No results" : "Your inbox is empty";
    public string EmptyBody => HasActiveSearchOrFilter
        ? "Try a different search or filter."
        : "Paste a link above to save something.";

    // Drives the page-level empty-state overlay. The state used to live in
    // CollectionView.EmptyView, but MAUI positions the EmptyView after the measured
    // header (rendering it low on screen) and re-measures it when the header animates
    // (the link-bar reveal), making it vanish. A Grid-sibling overlay is deterministic.
    public bool IsEmptyStateVisible => !IsLoading && SaveGroups.Count == 0;

    partial void OnIsLoadingChanged(bool value) =>
        OnPropertyChanged(nameof(IsEmptyStateVisible));

    partial void OnSaveGroupsChanged(System.Collections.ObjectModel.ObservableCollection<SaveGroup> value) =>
        OnPropertyChanged(nameof(IsEmptyStateVisible));

    public InboxViewModel(IApiService apiService, IScreenshotReviewService screenshotReviewService)
    {
        _apiService = apiService;
        _screenshotReviewService = screenshotReviewService;

        WeakReferenceMessenger.Default.Register<ScreenshotReviewCountChangedMessage>(
            this,
            (_, message) => UnreviewedScreenshotCount = message.Value);
    }

    partial void OnUnreviewedScreenshotCountChanged(int value)
    {
        OnPropertyChanged(nameof(HasUnreviewedScreenshots));
        OnPropertyChanged(nameof(UnreviewedScreenshotCountText));
    }

    partial void OnSearchQueryChanged(string value)
    {
        // No filtering while the user is typing: ANY list rebuild — even a granular
        // batch update — makes the CollectionView resign the search Entry's
        // keyboard (proven on-device). Filters apply on COMMIT (Done key or focus
        // loss) via CommitSearchCommand. An emptied query applies immediately so
        // the clear button restores the list without an extra tap.
        if (string.IsNullOrWhiteSpace(value))
            ApplyFiltersImmediately();
    }

    [RelayCommand]
    private void CommitSearch()
    {
        ApplyFiltersImmediately();
    }

    [RelayCommand]
    public async Task LoadSavesAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            var saves = await _apiService.GetSavesAsync(status: "active");
            _allSaves = saves.ToList();
            RebuildCategoryFilters();
            ApplyFiltersImmediately();
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch
        {
            ErrorMessage = "Couldn't load saves. Check your connection.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task PasteLinkAsync()
    {
        var url = LinkInput.Trim();
        if (string.IsNullOrEmpty(url)) return;

        try
        {
            var save = await _apiService.CreateSaveAsync("link", url);
            AddSaveAndRefresh(save);
            CollapseLinkBar();
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch
        {
            ErrorMessage = "Couldn't save the link. Check your connection.";
        }
    }

    [RelayCommand]
    private async Task PickAndUploadImageAsync()
    {
        FileResult? result;
        try
        {
            result = await MediaPicker.PickPhotoAsync(new MediaPickerOptions
            {
                Title = "Choose a screenshot to save"
            });
        }
        catch (Exception)
        {
            // User denied permission or picker not available on this platform
            ErrorMessage = "Photo library access is required to save screenshots.";
            return;
        }

        if (result is null) return;  // user cancelled

        IsUploadingImage = true;
        ErrorMessage = string.Empty;
        try
        {
            // Read bytes from the selected photo
            byte[] bytes;
            await using (var stream = await result.OpenReadAsync())
            {
                bytes = new byte[stream.Length];
                var totalRead = 0;
                while (totalRead < bytes.Length)
                {
                    var read = await stream.ReadAsync(bytes.AsMemory(totalRead));
                    if (read == 0) break;
                    totalRead += read;
                }
            }

            // Infer content type from file extension
            var ext = Path.GetExtension(result.FileName).ToLowerInvariant();
            var contentType = ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".heic"           => "image/heic",
                ".webp"           => "image/webp",
                _                 => "image/png"
            };

            // 1. Get a presigned PUT URL from the backend
            var presign = await _apiService.GetPresignedUploadUrlAsync(contentType);

            // 2. Upload bytes directly to R2 (backend never sees the image bytes)
            await _apiService.UploadBytesAsync(presign.UploadUrl, bytes, contentType);

            // 3. Create the save record referencing the uploaded key
            var save = await _apiService.CreateImageSaveAsync(presign.Key);
            AddSaveAndRefresh(save);
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (Exception)
        {
            ErrorMessage = "Couldn't upload screenshot. Check your connection.";
        }
        finally
        {
            IsUploadingImage = false;
        }
    }

    [RelayCommand]
    private void ToggleLinkBar()
    {
        if (IsLinkBarVisible)
        {
            CollapseLinkBar();
            return;
        }

        IsLinkBarVisible = true;
    }

    [RelayCommand]
    private async Task CompleteSaveAsync(long saveId)
    {
        try
        {
            await _apiService.CompleteSaveAsync(saveId);
            RemoveSaveFromGroups(saveId);
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task NavigateToDetailAsync(long saveId)
    {
        await Shell.Current.GoToAsync($"{nameof(Views.SaveDetailPage)}?saveId={saveId}");
    }

    [RelayCommand]
    private async Task OpenSettingsAsync()
    {
        await Shell.Current.GoToAsync(nameof(SettingsPage));
    }

    [RelayCommand]
    public async Task RefreshScreenshotCountAsync()
    {
        try
        {
            UnreviewedScreenshotCount = await _screenshotReviewService.GetUnreviewedCountAsync();
        }
        catch
        {
            UnreviewedScreenshotCount = 0;
        }
    }

    [RelayCommand]
    private async Task OpenScreenshotReviewAsync()
    {
        var state = await _screenshotReviewService.GetAuthorizationStateAsync();
        switch (state)
        {
            case ScreenshotPhotoAuthorizationState.Authorized:
                await _screenshotReviewService.CaptureBaselineIfNeededAsync();
                await Shell.Current.GoToAsync(nameof(ScreenshotsReviewPage));
                break;
            case ScreenshotPhotoAuthorizationState.NotDetermined:
                await Shell.Current.GoToAsync(nameof(ScreenshotPermissionPage));
                break;
            case ScreenshotPhotoAuthorizationState.Denied:
            case ScreenshotPhotoAuthorizationState.Restricted:
                await Shell.Current.DisplayAlertAsync(
                    "Photos access is off",
                    "Muninn needs Photos access to review new screenshots. You can enable access in iOS Settings.",
                    "Open Settings");
                AppInfo.Current.ShowSettingsUI();
                break;
            case ScreenshotPhotoAuthorizationState.Limited:
                await Shell.Current.DisplayAlertAsync(
                    "Full access needed",
                    "Limited Photos access only shows selected items. Muninn needs full access so it can watch for new screenshots after your baseline.",
                    "OK");
                break;
            default:
                await Shell.Current.DisplayAlertAsync(
                    "Not available",
                    "Automatic screenshot review is available on iOS.",
                    "OK");
                break;
        }
    }

    [RelayCommand]
    private async Task ArchiveSaveAsync(long saveId)
    {
        try
        {
            await _apiService.ArchiveSaveAsync(saveId);
            RemoveSaveFromGroups(saveId);
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private Task SelectCategoryAsync(CategoryFilterOption? option)
    {
        if (option is null) return Task.CompletedTask;

        SelectedCategory = option.Name;
        RebuildCategoryFilters();
        ApplyFiltersImmediately();
        return Task.CompletedTask;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void ApplyFilters()
    {
        var filtered = SelectedCategory == "All"
            ? _allSaves
            : _allSaves.Where(s => s.Category == SelectedCategory);

        var query = SearchQuery.Trim();
        if (!string.IsNullOrWhiteSpace(query))
        {
            filtered = filtered.Where(s => MatchesSearch(s, query));
        }

        RebuildGroups(filtered);
        OnPropertyChanged(nameof(HasActiveSearchOrFilter));
        OnPropertyChanged(nameof(EmptyTitle));
        OnPropertyChanged(nameof(EmptyBody));
    }

    private void RebuildGroups(IEnumerable<SaveModel> saves)
    {
        var grouped = saves
            .GroupBy(s => s.Category)
            .Select(g => new SaveGroup(g.Key, g))
            .ToList();

        Console.WriteLine("[SSDIAG] Inbox RebuildGroups (granular sync) BEGIN");

        // GRANULAR sync — never Clear(): Clear raises a Reset collection-changed
        // event, and on Reset the CollectionView does a full ReloadData, which
        // recreates the header and steals focus from the search Entry (keyboard
        // dismissed after every character — the earlier Clear+Add "in-place" fix
        // still Reset). Only individual remove/insert/move events fire here, which
        // the CollectionView applies as batch updates without touching the header.
        for (var i = SaveGroups.Count - 1; i >= 0; i--)
        {
            if (grouped.All(g => g.Category != SaveGroups[i].Category))
                SaveGroups.RemoveAt(i);
        }

        for (var i = 0; i < grouped.Count; i++)
        {
            var desired = grouped[i];
            var existingIndex = -1;
            for (var j = 0; j < SaveGroups.Count; j++)
            {
                if (SaveGroups[j].Category == desired.Category)
                {
                    existingIndex = j;
                    break;
                }
            }

            if (existingIndex < 0)
            {
                SaveGroups.Insert(Math.Min(i, SaveGroups.Count), desired);
                continue;
            }

            if (existingIndex != i && i < SaveGroups.Count)
                SaveGroups.Move(existingIndex, i);

            SyncGroupItems(SaveGroups[i], desired);
        }

        Console.WriteLine("[SSDIAG] Inbox RebuildGroups (granular sync) END");
        OnPropertyChanged(nameof(IsEmptyStateVisible));
    }

    /// <summary>Diffs one group's items into the live group — granular events only.</summary>
    private static void SyncGroupItems(SaveGroup target, SaveGroup desired)
    {
        for (var i = target.Count - 1; i >= 0; i--)
        {
            if (desired.All(s => s.Id != target[i].Id))
                target.RemoveAt(i);
        }

        for (var i = 0; i < desired.Count; i++)
        {
            var existingIndex = -1;
            for (var j = 0; j < target.Count; j++)
            {
                if (target[j].Id == desired[i].Id)
                {
                    existingIndex = j;
                    break;
                }
            }

            if (existingIndex < 0)
                target.Insert(Math.Min(i, target.Count), desired[i]);
            else if (existingIndex != i)
                target.Move(existingIndex, i);
        }
    }

    private void AddSaveAndRefresh(SaveModel save)
    {
        _allSaves.Insert(0, save);
        RebuildCategoryFilters();
        ApplyFiltersImmediately();
    }

    private void RemoveSaveFromGroups(long saveId)
    {
        _allSaves.RemoveAll(s => s.Id == saveId);
        RebuildCategoryFilters();
        ApplyFiltersImmediately();
    }

    private void CollapseLinkBar()
    {
        LinkInput = string.Empty;
        IsLinkBarVisible = false;
    }

    private void DebounceApplyFilters()
    {
        _searchDebounceCts?.Cancel();
        _searchDebounceCts?.Dispose();

        var cts = new CancellationTokenSource();
        _searchDebounceCts = cts;
        _ = ApplyFiltersAfterDelayAsync(cts.Token);
    }

    private async Task ApplyFiltersAfterDelayAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(SearchDebounceDelayMs, token);
            if (token.IsCancellationRequested)
                return;

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (!token.IsCancellationRequested)
                    ApplyFilters();
            });
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void ApplyFiltersImmediately()
    {
        _searchDebounceCts?.Cancel();
        _searchDebounceCts?.Dispose();
        _searchDebounceCts = null;
        ApplyFilters();
    }

    private void RebuildCategoryFilters()
    {
        var categories = _allSaves
            .Select(s => s.Category)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c)
            .ToList();

        if (SelectedCategory != "All" &&
            !categories.Contains(SelectedCategory, StringComparer.OrdinalIgnoreCase))
        {
            SelectedCategory = "All";
        }

        categories.Insert(0, "All");

        CategoryFilters = new ObservableCollection<CategoryFilterOption>(
            categories.Select(category =>
            {
                var isSelected = category == SelectedCategory;
                return new CategoryFilterOption(
                    category,
                    isSelected,
                    ResourceColor(isSelected ? "ColorAccent" : "ColorSunken"),
                    ResourceColor(isSelected ? "ColorSurface" : "ColorAccentTintText"));
            }));
    }

    private static Color ResourceColor(string key)
    {
        return Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color
            ? color
            : Colors.Transparent;
    }

    private static bool MatchesSearch(SaveModel save, string query)
    {
        return Contains(save.DisplayTitle, query)
            || Contains(save.Domain, query)
            || Contains(save.Analysis?.InferredIntent, query)
            || Contains(FirstLine(save.Analysis?.ExtractedText), query)
            || Contains(save.Category, query);
    }

    private static bool Contains(string? value, string query)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private static string? FirstLine(string? value)
    {
        return value?.Split('\n').FirstOrDefault();
    }
}
