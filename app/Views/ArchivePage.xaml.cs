using Muninn.ViewModels;

namespace Muninn.Views;

public partial class ArchivePage : ContentPage
{
    private readonly ArchiveViewModel _viewModel;

    public ArchivePage(ArchiveViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadArchiveCommand.ExecuteAsync(null);
    }
}
