using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Graphics;
using Muninn.Models;
using Muninn.Services;
using Muninn.Views;
using System.Collections.ObjectModel;
using System.Threading;

namespace Muninn.ViewModels;

public partial class ArchiveViewModel : BaseViewModel
{
    private const int SearchDebounceDelayMs = 200;

    private readonly IApiService _apiService;
    private List<SaveModel> _allSaves = [];
    private CancellationTokenSource? _searchDebounceCts;

    [ObservableProperty]
    private ObservableCollection<SaveModel> _saves = [];

    [ObservableProperty]
    private ObservableCollection<CategoryFilterOption> _categoryFilters = [];

    [ObservableProperty]
    private string _selectedCategory = "All";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _selectedArchiveTab = "archived";

    public Color ArchivedTabBackgroundColor => TabBackgroundColor("archived");
    public Color ArchivedTabTextColor => TabTextColor("archived");
    public Color CompletedTabBackgroundColor => TabBackgroundColor("completed");
    public Color CompletedTabTextColor => TabTextColor("completed");
    public bool HasActiveSearchOrFilter => !string.IsNullOrWhiteSpace(SearchQuery) || SelectedCategory != "All";
    public string EmptyTitle => HasActiveSearchOrFilter
        ? "No results"
        : SelectedArchiveTab == "archived" ? "Nothing archived yet" : "Nothing completed yet";
    public string EmptyBody => SelectedArchiveTab == "archived"
        ? HasActiveSearchOrFilter ? "Try a different search or filter." : "Archived saves rest here until you need them again."
        : HasActiveSearchOrFilter ? "Try a different search or filter." : "Completed saves move here so your inbox stays focused.";
    public string UndoActionText => SelectedArchiveTab == "archived" ? "Unarchive" : "Uncomplete";

    // Drives the page-level empty-state overlay (replaces CollectionView.EmptyView,
    // which MAUI positions after the measured header — see InboxViewModel).
    public bool IsEmptyStateVisible => !IsLoading && Saves.Count == 0;

    partial void OnIsLoadingChanged(bool value) =>
        OnPropertyChanged(nameof(IsEmptyStateVisible));

    partial void OnSavesChanged(ObservableCollection<SaveModel> value) =>
        OnPropertyChanged(nameof(IsEmptyStateVisible));

    public ArchiveViewModel(IApiService apiService)
    {
        _apiService = apiService;
    }

    /// <summary>
    /// Loads archived or completed saves, newest-first (completed-at or created-at desc).
    /// </summary>
    [RelayCommand]
    public async Task LoadArchiveAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            var saves = await _apiService.GetSavesAsync(status: SelectedArchiveTab);
            // Prefer sorting by completed-at (when the user acted on it) so recently
            // completed saves rise to the top; fall back to created-at.
            var ordered = saves.OrderByDescending(s => s.CompletedAt ?? s.CreatedAt);
            _allSaves = ordered.ToList();
            RebuildCategoryFilters();
            ApplyFiltersImmediately();
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch
        {
            ErrorMessage = "Couldn't load the archive. Check your connection.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task NavigateToDetailAsync(long saveId)
    {
        await Shell.Current.GoToAsync($"{nameof(SaveDetailPage)}?saveId={saveId}");
    }

    [RelayCommand]
    private async Task SelectArchivedTabAsync()
    {
        if (SelectedArchiveTab == "archived") return;
        SelectedArchiveTab = "archived";
        await LoadArchiveAsync();
    }

    [RelayCommand]
    private async Task SelectCompletedTabAsync()
    {
        if (SelectedArchiveTab == "completed") return;
        SelectedArchiveTab = "completed";
        await LoadArchiveAsync();
    }

    [RelayCommand]
    private async Task UndoSaveAsync(long saveId)
    {
        try
        {
            if (SelectedArchiveTab == "archived")
                await _apiService.UnarchiveSaveAsync(saveId);
            else
                await _apiService.UncompleteSaveAsync(saveId);

            _allSaves.RemoveAll(s => s.Id == saveId);
            RebuildCategoryFilters();
            ApplyFiltersImmediately();
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch
        {
            ErrorMessage = "Couldn't move this save back to Inbox.";
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

    partial void OnSelectedArchiveTabChanged(string value)
    {
        OnPropertyChanged(nameof(ArchivedTabBackgroundColor));
        OnPropertyChanged(nameof(ArchivedTabTextColor));
        OnPropertyChanged(nameof(CompletedTabBackgroundColor));
        OnPropertyChanged(nameof(CompletedTabTextColor));
        OnPropertyChanged(nameof(EmptyTitle));
        OnPropertyChanged(nameof(EmptyBody));
        OnPropertyChanged(nameof(UndoActionText));
    }

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

        // GRANULAR sync — never Clear(): Clear raises Reset → full ReloadData →
        // header recreated → search Entry loses focus (keyboard dismissed per
        // character). Individual remove/insert/move events only (see InboxViewModel).
        var desired = filtered.ToList();
        for (var i = Saves.Count - 1; i >= 0; i--)
        {
            if (desired.All(s => s.Id != Saves[i].Id))
                Saves.RemoveAt(i);
        }

        for (var i = 0; i < desired.Count; i++)
        {
            var existingIndex = -1;
            for (var j = 0; j < Saves.Count; j++)
            {
                if (Saves[j].Id == desired[i].Id)
                {
                    existingIndex = j;
                    break;
                }
            }

            if (existingIndex < 0)
                Saves.Insert(Math.Min(i, Saves.Count), desired[i]);
            else if (existingIndex != i)
                Saves.Move(existingIndex, i);
        }

        OnPropertyChanged(nameof(IsEmptyStateVisible));
        OnPropertyChanged(nameof(HasActiveSearchOrFilter));
        OnPropertyChanged(nameof(EmptyTitle));
        OnPropertyChanged(nameof(EmptyBody));
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

    private Color TabBackgroundColor(string tab)
    {
        return ResourceColor(SelectedArchiveTab == tab ? "ColorAccent" : "ColorSunken");
    }

    private Color TabTextColor(string tab)
    {
        return ResourceColor(SelectedArchiveTab == tab ? "ColorSurface" : "ColorAccentTintText");
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
