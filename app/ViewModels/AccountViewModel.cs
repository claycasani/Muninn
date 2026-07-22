using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Muninn.Services;
using Muninn.Views;
using System.Globalization;

namespace Muninn.ViewModels;

public partial class AccountViewModel : BaseViewModel
{
    private readonly IApiService _apiService;

    [ObservableProperty]
    private string _displayName = "Muninn User";

    [ObservableProperty]
    private string _editableDisplayName = string.Empty;

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _initials = "M";

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _successMessage = string.Empty;

    public AccountViewModel(IApiService apiService)
    {
        _apiService = apiService;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        ErrorMessage = string.Empty;
        SuccessMessage = string.Empty;
        try
        {
            var account = await _apiService.GetAccountAsync();
            ApplyAccount(account);
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
            ApplyEmail(await JwtHelper.GetStoredEmailAsync());
        }
        catch
        {
            ApplyEmail(await JwtHelper.GetStoredEmailAsync());
        }
    }

    [RelayCommand]
    private async Task NavigateToChangePasswordAsync()
    {
        await Shell.Current.GoToAsync(nameof(ChangePasswordPage));
    }

    [RelayCommand]
    private async Task NavigateToDeleteAccountAsync()
    {
        await Shell.Current.GoToAsync(nameof(DeleteAccountPage));
    }

    [RelayCommand]
    private async Task SaveDisplayNameAsync()
    {
        ErrorMessage = string.Empty;
        SuccessMessage = string.Empty;

        var name = EditableDisplayName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            ErrorMessage = "Display name is required.";
            return;
        }

        if (name.Length > 80)
        {
            ErrorMessage = "Display name must be 80 characters or fewer.";
            return;
        }

        try
        {
            var account = await _apiService.UpdateAccountAsync(new AccountUpdateRequest(DisplayName: name));
            ApplyAccount(account);
            SuccessMessage = "Display name updated.";
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch
        {
            ErrorMessage = "Couldn't update display name.";
        }
    }

    private void ApplyEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return;

        Email = email;
        DisplayName = DisplayNameFromEmail(email);
        EditableDisplayName = DisplayName;
        Initials = InitialsFromName(DisplayName);
    }

    private void ApplyAccount(AccountResponse account)
    {
        Email = account.Email;
        DisplayName = string.IsNullOrWhiteSpace(account.DisplayName)
            ? DisplayNameFromEmail(account.Email)
            : account.DisplayName;
        EditableDisplayName = DisplayName;
        Initials = InitialsFromName(DisplayName);
    }

    private static string DisplayNameFromEmail(string email)
    {
        var local = email.Split('@')[0]
            .Replace('.', ' ')
            .Replace('_', ' ')
            .Replace('-', ' ');

        var textInfo = CultureInfo.CurrentCulture.TextInfo;
        return string.IsNullOrWhiteSpace(local)
            ? email
            : textInfo.ToTitleCase(local.ToLowerInvariant());
    }

    private static string InitialsFromName(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "M";
        if (parts.Length == 1) return parts[0][..Math.Min(2, parts[0].Length)].ToUpperInvariant();
        return string.Concat(parts.Take(2).Select(p => char.ToUpperInvariant(p[0])));
    }
}
