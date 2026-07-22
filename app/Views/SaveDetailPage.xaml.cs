using Muninn.ViewModels;

namespace Muninn.Views;

public partial class SaveDetailPage : ContentPage
{
    private readonly SaveDetailViewModel _viewModel;

    public SaveDetailPage(SaveDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }

    private async void OnSavePreviewTapped(object? sender, TappedEventArgs e)
    {
        if (SavePreviewImage.Source is null) return;
        await Navigation.PushModalAsync(new ImagePreviewPage(SavePreviewImage.Source));
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        // DisplayActionSheet renders a native iOS 26 action sheet with Liquid Glass
        // styling, system-red destructive button, and blurred backdrop automatically.
        var result = await DisplayActionSheetAsync(
            "Delete this save?",
            "Cancel",
            "Delete Entry");

        if (result == "Delete Entry")
            await _viewModel.DeleteCommand.ExecuteAsync(null);
    }
}
