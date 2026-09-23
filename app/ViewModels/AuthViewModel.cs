using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Muninn.Services;

namespace Muninn.ViewModels;

public partial class AuthViewModel : BaseViewModel
{
    private readonly IApiService _apiService;
    private readonly IScreenshotReviewService _screenshotReviewService;
    private readonly ILocalNotificationService _localNotificationService;

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isRegistering = false;

    // Derived label text — bound in XAML
    public string ActionButtonText => IsRegistering ? "Create account" : "Sign in";
    public string ToggleText => IsRegistering ? "Already have an account? " : "Don't have an account? ";
    public string ToggleLinkText => IsRegistering ? "Sign in" : "Register";

    partial void OnIsRegisteringChanged(bool value)
    {
        OnPropertyChanged(nameof(ActionButtonText));
        OnPropertyChanged(nameof(ToggleText));
        OnPropertyChanged(nameof(ToggleLinkText));
        ErrorMessage = string.Empty;
    }

    public AuthViewModel(
        IApiService apiService,
        IScreenshotReviewService screenshotReviewService,
        ILocalNotificationService localNotificationService)
    {
        _apiService = apiService;
        _screenshotReviewService = screenshotReviewService;
        _localNotificationService = localNotificationService;
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        ErrorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Please enter your email and password.";
            return;
        }

        try
        {
            string token = IsRegistering
                ? await _apiService.RegisterAsync(Email.Trim(), Password)
                : await _apiService.LoginAsync(Email.Trim(), Password);

            // SecureStorage can fail on Mac Catalyst in debug (keychain not accessible
            // without a proper provisioning profile). Fall back to Preferences.
            try { await SecureStorage.SetAsync("auth_token", token); }
            catch { Preferences.Set("auth_token_fallback", token); }

            _apiService.SetAuthToken(token);

            // Account settings are server-owned. Sync them after authentication so the
            // default local reminder is created on a fresh beta install, while an
            // existing account's disabled/time settings are respected.
            try
            {
                var account = await _apiService.GetAccountAsync();
                await _localNotificationService.SyncDailyDigestAsync(
                    account.DailyDigestEnabled,
                    account.NotificationsEnabled,
                    account.DigestTime);
            }
            catch
            {
                // The reminder is best-effort and must not prevent the user from
                // entering the app if settings cannot be loaded during login.
            }

            // Process any URL queued by the iOS Share Extension.  Runs before navigation
            // so InboxPage.OnAppearing sees the new save when LoadSavesAsync fires.
            // Swallow errors — a network failure leaves the key intact for the next session.
            try { await _apiService.ProcessPendingShareAsync(); } catch { }

            // If AuthPage was presented as a modal (mid-session expiry path), pop it
            // first so Shell.GoToAsync doesn't fight with the modal stack.
            if (Shell.Current.Navigation.ModalStack.Count > 0)
                await Shell.Current.Navigation.PopModalAsync(animated: false);

            if (await _screenshotReviewService.ShouldShowPrimingAutomaticallyAsync())
                await Shell.Current.GoToAsync(nameof(Views.ScreenshotPermissionPage));
            else
                await Shell.Current.GoToAsync("//InboxPage");
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (Exception ex)
        {
#if DEBUG
            ErrorMessage = $"Debug: {ex.GetType().Name}: {ex.Message}";
#else
            ErrorMessage = "Something went wrong. Please try again.";
#endif
        }
    }

    [RelayCommand]
    private void ToggleMode()
    {
        IsRegistering = !IsRegistering;
        Password = string.Empty;
    }
}
