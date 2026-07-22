using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Muninn.Models;
using Muninn.Services;

namespace Muninn.ViewModels;

public partial class ManageCategoriesViewModel : BaseViewModel
{
    private readonly IApiService _apiService;

    [ObservableProperty]
    private ObservableCollection<CategoryModel> _categories = [];

    [ObservableProperty]
    private string _newCategoryName = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));

    public ManageCategoriesViewModel(IApiService apiService)
    {
        _apiService = apiService;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            var categories = await _apiService.GetCategoriesAsync();
            Categories = new ObservableCollection<CategoryModel>(categories);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        var name = NewCategoryName?.Trim();
        if (string.IsNullOrEmpty(name)) return;

        try
        {
            ErrorMessage = string.Empty;
            var created = await _apiService.CreateCategoryAsync(name);
            Categories.Add(created);
            NewCategoryName = string.Empty;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    /// <summary>Called by the page after collecting the new name from a prompt.</summary>
    public async Task RenameAsync(CategoryModel category, string? newName)
    {
        var trimmed = newName?.Trim();
        if (category is null || string.IsNullOrEmpty(trimmed) || trimmed == category.Name) return;

        try
        {
            ErrorMessage = string.Empty;
            var updated = await _apiService.RenameCategoryAsync(category.Id, trimmed);
            category.Name = updated.Name;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    /// <summary>Called by the page after the user confirms deletion.</summary>
    public async Task DeleteAsync(CategoryModel category)
    {
        if (category is null) return;

        try
        {
            ErrorMessage = string.Empty;
            await _apiService.DeleteCategoryByIdAsync(category.Id);
            Categories.Remove(category);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}
