using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Muninn.Services;
using Muninn.Views;

namespace Muninn.ViewModels;

public partial class DeleteAccountViewModel : BaseViewModel
{
    private readonly IApiService _apiService;

    [ObservableProperty]
    private bool _isDeleting;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public DeleteAccountViewModel(IApiService apiService)
    {
        _apiService = apiService;
    }

    [RelayCommand]
    private async Task DeleteAccountAsync()
    {
        IsDeleting = true;
        ErrorMessage = string.Empty;

        try
        {
            await _apiService.DeleteAccountAsync();
            _apiService.ClearAuthToken();
            App.ClearStoredToken();

            if (Shell.Current.Navigation.ModalStack.Count > 0)
                await Shell.Current.Navigation.PopModalAsync(animated: false);

            await Shell.Current.GoToAsync(nameof(AuthPage));
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch
        {
            ErrorMessage = "Couldn't delete your account. Try again.";
        }
        finally
        {
            IsDeleting = false;
        }
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        await Shell.Current.GoToAsync("..");
    }
}
