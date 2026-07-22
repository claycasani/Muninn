using Muninn.Models;
using Muninn.ViewModels;

namespace Muninn.Views;

public partial class ManageCategoriesPage : ContentPage
{
    private readonly ManageCategoriesViewModel _viewModel;

    public ManageCategoriesPage(ManageCategoriesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_viewModel.Categories.Count == 0)
            await _viewModel.LoadCommand.ExecuteAsync(null);
    }

    private async void OnRenameClicked(object? sender, EventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not CategoryModel category)
            return;

        var newName = await DisplayPromptAsync(
            "Rename category",
            "Every save in this category will move to the new name.",
            accept: "Rename",
            cancel: "Cancel",
            initialValue: category.Name,
            maxLength: 80);

        if (!string.IsNullOrWhiteSpace(newName))
            await _viewModel.RenameAsync(category, newName);
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not CategoryModel category)
            return;

        var confirmed = await DisplayAlert(
            $"Delete “{category.Name}”?",
            "Saves in this category will be re-sorted by AI into your other categories. This can take a moment.",
            "Delete",
            "Cancel");

        if (confirmed)
            await _viewModel.DeleteAsync(category);
    }
}
