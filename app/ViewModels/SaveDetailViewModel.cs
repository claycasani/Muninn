using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Muninn.Models;
using Muninn.Services;

namespace Muninn.ViewModels;

[QueryProperty(nameof(SaveId), "saveId")]
public partial class SaveDetailViewModel : BaseViewModel
{
    private readonly IApiService _apiService;

    [ObservableProperty]
    private long _saveId;

    [ObservableProperty]
    private SaveModel? _save;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    /// True once CompleteCommand or ArchiveCommand succeeds — used to pop back.
    [ObservableProperty]
    private bool _didAct;

    [ObservableProperty]
    private bool _isDeleting;

    // Category picker (bottom-sheet) state.
    [ObservableProperty]
    private ObservableCollection<PickerCategory> _pickerCategories = [];

    [ObservableProperty]
    private bool _isCategoryPickerVisible;

    // The category the user has tapped (highlighted green) but not yet confirmed.
    private string? _pendingCategory;

    public SaveDetailViewModel(IApiService apiService)
    {
        _apiService = apiService;
    }

    public bool HasImagePreview => Save?.IsImageSave == true && !string.IsNullOrWhiteSpace(Save?.ImageUrl);
    public bool HasLinkPreview => !string.IsNullOrWhiteSpace(Save?.SourceUrl);
    public string MetaLabel => Save?.IsImageSave == true ? "Screenshot" : Save?.Domain ?? string.Empty;

    partial void OnSaveChanged(SaveModel? value)
    {
        OnPropertyChanged(nameof(HasImagePreview));
        OnPropertyChanged(nameof(HasLinkPreview));
        OnPropertyChanged(nameof(MetaLabel));
    }

    // Called by the page's OnAppearing whenever SaveId is set.
    [RelayCommand]
    public async Task LoadAsync()
    {
        if (SaveId <= 0) return;

        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            Save = await _apiService.GetSaveByIdAsync(SaveId);
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.StatusCode == 404
                ? "This save no longer exists."
                : ex.Message;
        }
        catch
        {
            ErrorMessage = "Couldn't load the save. Check your connection.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task CompleteAsync()
    {
        if (Save is null) return;
        try
        {
            await _apiService.CompleteSaveAsync(Save.Id);
            await Shell.Current.GoToAsync("..");
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch
        {
            ErrorMessage = "Couldn't complete this save. Try again.";
        }
    }

    [RelayCommand]
    private async Task ArchiveAsync()
    {
        if (Save is null) return;
        try
        {
            await _apiService.ArchiveSaveAsync(Save.Id);
            await Shell.Current.GoToAsync("..");
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch
        {
            ErrorMessage = "Couldn't archive this save. Try again.";
        }
    }

    [RelayCommand]
    private async Task UncompleteAsync()
    {
        if (Save is null) return;
        try
        {
            await _apiService.UncompleteSaveAsync(Save.Id);
            await Shell.Current.GoToAsync("..");
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch
        {
            ErrorMessage = "Couldn't move this save back to Inbox. Try again.";
        }
    }

    [RelayCommand]
    private async Task UnarchiveAsync()
    {
        if (Save is null) return;
        try
        {
            await _apiService.UnarchiveSaveAsync(Save.Id);
            await Shell.Current.GoToAsync("..");
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch
        {
            ErrorMessage = "Couldn't move this save back to Inbox. Try again.";
        }
    }

    [RelayCommand]
    private async Task ChangeCategoryAsync()
    {
        if (Save is null) return;

        try
        {
            // Pull the user's real category list (includes categories with no saves
            // yet, like a freshly-added "Nightlife") — not just categories derived
            // from existing saves.
            var categories = await _apiService.GetCategoriesAsync();
            _pendingCategory = Save.Category;
            PickerCategories = new ObservableCollection<PickerCategory>(
                categories.Select(c => new PickerCategory(
                    c.Name, string.Equals(c.Name, Save.Category, StringComparison.OrdinalIgnoreCase))));
            IsCategoryPickerVisible = true;
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch
        {
            ErrorMessage = "Couldn't load categories. Try again.";
        }
    }

    /// <summary>Closes without applying (X / Cancel).</summary>
    [RelayCommand]
    private void CloseCategoryPicker() => IsCategoryPickerVisible = false;

    /// <summary>Highlights a chip green. Does NOT apply or close — the user confirms.</summary>
    [RelayCommand]
    private void SelectCategory(PickerCategory? chip)
    {
        if (chip is null) return;
        foreach (var c in PickerCategories)
            c.IsSelected = ReferenceEquals(c, chip);
        _pendingCategory = chip.Name;
    }

    /// <summary>Applies the highlighted category and closes.</summary>
    [RelayCommand]
    private async Task ConfirmCategoryAsync()
    {
        var category = _pendingCategory?.Trim();
        IsCategoryPickerVisible = false;
        if (Save is null || string.IsNullOrWhiteSpace(category) ||
            string.Equals(category, Save.Category, StringComparison.OrdinalIgnoreCase))
            return;

        try
        {
            Save = await _apiService.UpdateSaveCategoryAsync(Save.Id, category);
        }
        catch (ApiException ex) { ErrorMessage = ex.Message; }
        catch { ErrorMessage = "Couldn't update this category. Try again."; }
    }

    /// <summary>Prompts for a new category, creates it, and selects it (stays open).</summary>
    [RelayCommand]
    private async Task AddCategoryAsync()
    {
        if (Save is null) return;

        var name = await Shell.Current.DisplayPromptAsync(
            "New Category",
            "Name this category",
            accept: "Add",
            cancel: "Cancel",
            placeholder: "Category name",
            maxLength: 80);

        name = name?.Trim();
        if (string.IsNullOrWhiteSpace(name)) return;

        try
        {
            // Create the category so it joins the managed list + AI vocabulary.
            // A duplicate name is fine — just select the existing one.
            try { await _apiService.CreateCategoryAsync(name); } catch (ApiException) { /* already exists */ }

            var existing = PickerCategories.FirstOrDefault(
                c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                existing = new PickerCategory(name, false);
                PickerCategories.Add(existing);
            }
            SelectCategory(existing); // highlight it; user still confirms
        }
        catch (ApiException ex) { ErrorMessage = ex.Message; }
        catch { ErrorMessage = "Couldn't add this category. Try again."; }
    }

    [RelayCommand]
    private async Task OpenUrlAsync()
    {
        var url = Save?.SourceUrl;
        if (!string.IsNullOrEmpty(url) && Uri.TryCreate(url, UriKind.Absolute, out var uri))
            await Launcher.OpenAsync(uri);
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (Save is null || IsDeleting) return;

        IsDeleting = true;
        ErrorMessage = string.Empty;
        try
        {
            await _apiService.DeleteSaveAsync(Save.Id);
            await Shell.Current.GoToAsync("..");
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch
        {
            ErrorMessage = "Couldn't delete this save. Try again.";
        }
        finally
        {
            IsDeleting = false;
        }
    }
}
