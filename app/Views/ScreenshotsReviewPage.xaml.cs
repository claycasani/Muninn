using Muninn.ViewModels;

using Muninn.Models;
using Muninn.Services;

namespace Muninn.Views;

public partial class ScreenshotsReviewPage : ContentPage
{
    private readonly ScreenshotsReviewViewModel _viewModel;
    private readonly IScreenshotReviewService _screenshotReviewService;

    public ScreenshotsReviewPage(
        ScreenshotsReviewViewModel viewModel,
        IScreenshotReviewService screenshotReviewService)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;
        _screenshotReviewService = screenshotReviewService;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private async void OnPreviewTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not BindableObject bindable ||
            bindable.BindingContext is not ScreenshotCandidate candidate)
        {
            return;
        }

        try
        {
            var imageData = await _screenshotReviewService.LoadImageDataAsync(candidate.LocalIdentifier);
            var source = ImageSource.FromStream(() => new MemoryStream(imageData.Bytes));
            await Navigation.PushModalAsync(new ImagePreviewPage(source));
        }
        catch
        {
            if (candidate.HasThumbnail)
                await Navigation.PushModalAsync(new ImagePreviewPage(candidate.ThumbnailSource));
        }
    }
}
