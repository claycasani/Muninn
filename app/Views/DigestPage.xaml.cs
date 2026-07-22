using Muninn.ViewModels;

namespace Muninn.Views;

public partial class DigestPage : ContentPage
{
    private readonly DigestViewModel _viewModel;

    public DigestPage(DigestViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadDigestCommand.ExecuteAsync(null);
    }
}
