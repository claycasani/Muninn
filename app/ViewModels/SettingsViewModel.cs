using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Muninn.Services;
using Muninn.Views;
using System.Globalization;

namespace Muninn.ViewModels;

public partial class SettingsViewModel : BaseViewModel
{
    private readonly IApiService _apiService;
    private bool _isHydrating;

    [ObservableProperty]
    private string _displayName = "Muninn User";

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _initials = "M";

    [ObservableProperty]
    private bool _dailyDigestEnabled = true;

    [ObservableProperty]
    private string _digestTime = "7:00 AM";

    [ObservableProperty]
    private string _digestTimeValue = "07:00";

    [ObservableProperty]
    private bool _notificationsEnabled = true;

    [ObservableProperty]
    private int _autoArchiveDays = 30;

    [ObservableProperty]
    private string _autoArchiveLabel = "After 30 days";

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public SettingsViewModel(IApiService apiService)
    {
        _apiService = apiService;
    }

    partial void OnDailyDigestEnabledChanged(bool value)
    {
        if (!_isHydrating)
            _ = SaveSettingsAsync(new AccountUpdateRequest(DailyDigestEnabled: value));
    }

    partial void OnNotificationsEnabledChanged(bool value)
    {
        if (!_isHydrating)
            _ = SaveSettingsAsync(new AccountUpdateRequest(NotificationsEnabled: value));
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        ErrorMessage = string.Empty;
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
            ErrorMessage = "Couldn't load settings.";
            ApplyEmail(await JwtHelper.GetStoredEmailAsync());
        }
    }

    [RelayCommand]
    private async Task ChangeDigestTimeAsync()
    {
        var choice = await Shell.Current.DisplayActionSheetAsync(
            "Digest Time",
            "Cancel",
            null,
            "7:00 AM",
            "8:00 AM",
            "12:00 PM",
            "6:00 PM",
            "9:00 PM");

        var value = choice switch
        {
            "7:00 AM" => "07:00",
            "8:00 AM" => "08:00",
            "12:00 PM" => "12:00",
            "6:00 PM" => "18:00",
            "9:00 PM" => "21:00",
            _ => null
        };

        if (value is null) return;
        await SaveSettingsAsync(new AccountUpdateRequest(DigestTime: value));
    }

    [RelayCommand]
    private async Task ChangeAutoArchiveAsync()
    {
        var choice = await Shell.Current.DisplayActionSheetAsync(
            "Auto-archive completed saves",
            "Cancel",
            null,
            "After 7 days",
            "After 14 days",
            "After 30 days",
            "After 60 days",
            "After 90 days");

        var value = choice switch
        {
            "After 7 days" => 7,
            "After 14 days" => 14,
            "After 30 days" => 30,
            "After 60 days" => 60,
            "After 90 days" => 90,
            _ => 0
        };

        if (value == 0) return;
        await SaveSettingsAsync(new AccountUpdateRequest(AutoArchiveDays: value));
    }

    [RelayCommand]
    private async Task NavigateToAccountAsync()
    {
        await Shell.Current.GoToAsync(nameof(AccountPage));
    }

    [RelayCommand]
    private async Task NavigateToCategoriesAsync()
    {
        await Shell.Current.GoToAsync(nameof(ManageCategoriesPage));
    }

    [RelayCommand]
    private async Task SignOutAsync()
    {
        _apiService.ClearAuthToken();
        App.ClearStoredToken();

        if (Shell.Current.Navigation.ModalStack.Count > 0)
            await Shell.Current.Navigation.PopModalAsync(animated: false);

        await Shell.Current.GoToAsync(nameof(AuthPage));
    }

    private void ApplyEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return;

        Email = email;
        DisplayName = DisplayNameFromEmail(email);
        Initials = InitialsFromName(DisplayName);
    }

    private void ApplyAccount(AccountResponse account)
    {
        _isHydrating = true;
        try
        {
            Email = account.Email;
            DisplayName = string.IsNullOrWhiteSpace(account.DisplayName)
                ? DisplayNameFromEmail(account.Email)
                : account.DisplayName;
            Initials = InitialsFromName(DisplayName);
            DailyDigestEnabled = account.DailyDigestEnabled;
            DigestTimeValue = account.DigestTime;
            DigestTime = FormatDigestTime(account.DigestTime);
            NotificationsEnabled = account.NotificationsEnabled;
            AutoArchiveDays = account.AutoArchiveDays;
            AutoArchiveLabel = $"After {AutoArchiveDays} days";
        }
        finally
        {
            _isHydrating = false;
        }
    }

    private async Task SaveSettingsAsync(AccountUpdateRequest request)
    {
        ErrorMessage = string.Empty;
        try
        {
            var updated = await _apiService.UpdateAccountAsync(request);
            ApplyAccount(updated);
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch
        {
            ErrorMessage = "Couldn't update settings.";
        }
    }

    private static string FormatDigestTime(string value)
    {
        if (!TimeOnly.TryParse(value, out var time)) return value;
        return time.ToString("h:mm tt", CultureInfo.CurrentCulture);
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
