using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Muninn.Models;
using Muninn.Services;
using Muninn.Views;
using System.Collections.ObjectModel;

namespace Muninn.ViewModels;

public partial class DigestViewModel : BaseViewModel
{
    private readonly IApiService _apiService;

    [ObservableProperty]
    private ObservableCollection<SaveModel> _saves = [];

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    /// <summary>Today's date, e.g. "Friday, June 13" — shown under the large title.</summary>
    public string DateSubtitle => DateTime.Now.ToString("dddd, MMMM d");

    // Drives the page-level empty-state overlay (replaces CollectionView.EmptyView,
    // which MAUI positions after the measured header — see InboxViewModel).
    public bool IsEmptyStateVisible => !IsLoading && Saves.Count == 0;

    partial void OnIsLoadingChanged(bool value) =>
        OnPropertyChanged(nameof(IsEmptyStateVisible));

    partial void OnSavesChanged(ObservableCollection<SaveModel> value) =>
        OnPropertyChanged(nameof(IsEmptyStateVisible));

    public DigestViewModel(IApiService apiService)
    {
        _apiService = apiService;
    }

    /// <summary>
    /// Loads today's digest. There is no dedicated /digest endpoint yet (the MVP is an
    /// in-app digest *view*), so this resurfaces the user's active saves, newest first.
    /// </summary>
    [RelayCommand]
    public async Task LoadDigestAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            var saves = await _apiService.GetSavesAsync(status: "active");
            var ordered = saves.OrderByDescending(s => s.CreatedAt);
            Saves = new ObservableCollection<SaveModel>(ordered);
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch
        {
            ErrorMessage = "Couldn't load your digest. Check your connection.";
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
}
