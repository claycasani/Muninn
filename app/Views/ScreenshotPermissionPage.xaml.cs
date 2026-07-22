using Muninn.ViewModels;

namespace Muninn.Views;

public partial class ScreenshotPermissionPage : ContentPage
{
    public ScreenshotPermissionPage(ScreenshotPermissionViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}

