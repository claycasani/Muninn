using Muninn.ViewModels;

namespace Muninn.Views;

public partial class DeleteAccountPage : ContentPage
{
    public DeleteAccountPage(DeleteAccountViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
