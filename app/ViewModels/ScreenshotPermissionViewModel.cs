using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Muninn.Diagnostics;
using Muninn.Services;
using Muninn.Views;

namespace Muninn.ViewModels;

public partial class ScreenshotPermissionViewModel : BaseViewModel
{
    private readonly IScreenshotReviewService _screenshotReviewService;

    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    private bool _isRequesting;

    public ScreenshotPermissionViewModel(IScreenshotReviewService screenshotReviewService)
    {
        _screenshotReviewService = screenshotReviewService;
    }

    [RelayCommand]
    private async Task AllowAccessAsync()
    {
        if (IsRequesting) return;

        IsRequesting = true;
        Message = string.Empty;

        try
        {
            Console.WriteLine("[SSDIAG] AllowAccessAsync: requesting authorization");
            var state = await _screenshotReviewService.RequestFullAccessAsync();
            Console.WriteLine($"[SSDIAG] AllowAccessAsync: authorization returned {state} (main thread: {MainThread.IsMainThread})");
            switch (state)
            {
                case ScreenshotPhotoAuthorizationState.Authorized:
                    if (LaunchDiagnostics.GrantNoInboxNav)
                    {
                        // Isolation: grant + baseline happened, but skip the first Inbox
                        // render. Staying here proves whether the respring is the Photos
                        // grant path or the Inbox render that normally follows it.
                        Console.WriteLine("[SSDIAG] AllowAccessAsync: GrantNoInboxNav set — staying on permission page, NOT navigating to Inbox");
                        Message = "Access granted. (Diagnostic: Inbox navigation suppressed.)";
                        break;
                    }
                    Console.WriteLine("[SSDIAG] AllowAccessAsync: navigating to //InboxPage");
                    await Shell.Current.GoToAsync("//InboxPage");
                    Console.WriteLine("[SSDIAG] AllowAccessAsync: //InboxPage navigation returned");
                    break;
                case ScreenshotPhotoAuthorizationState.Limited:
                    Message = "Muninn needs full Photos access for automatic screenshot review. Limited access will not watch for new screenshots.";
                    break;
                case ScreenshotPhotoAuthorizationState.Denied:
                case ScreenshotPhotoAuthorizationState.Restricted:
                    Message = "Photos access is off. You can enable full access later from iOS Settings or by tapping the screenshot button in Inbox.";
                    break;
                default:
                    Message = "Screenshot review was not enabled.";
                    break;
            }
        }
        finally
        {
            IsRequesting = false;
        }
    }

    [RelayCommand]
    private async Task NotNowAsync()
    {
        await _screenshotReviewService.DeferPrimingAsync();
        await Shell.Current.GoToAsync("//InboxPage");
    }
}

