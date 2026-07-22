using Muninn.Services;
using Muninn.Views;
using CommunityToolkit.Mvvm.Messaging;
using Muninn.Diagnostics;

namespace Muninn;

public partial class App : Application
{
    private readonly IApiService _apiService;
    private readonly IScreenshotReviewService _screenshotReviewService;
    private readonly IServiceProvider _services;

    // True when any window-level diagnostic mode replaces AppShell. Startup work
    // (auth check, share processing, count publishing) assumes the real AppShell
    // routes exist and must not run in these modes.
    private static bool DiagnosticWindowMode =>
        LaunchDiagnostics.SafeLaunch ||
        LaunchDiagnostics.MinimalShell ||
        LaunchDiagnostics.MinimalTabs ||
        LaunchDiagnostics.NoShellInbox;

    public App(IApiService apiService, IScreenshotReviewService screenshotReviewService,
        IServiceProvider services)
    {
        InitializeComponent();
        _apiService = apiService;
        _screenshotReviewService = screenshotReviewService;
        _services = services;
        UserAppTheme = AppTheme.Light;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        Console.WriteLine($"[SSDIAG] Launch diagnostics: {LaunchDiagnostics.Summary}");
        System.Diagnostics.Debug.WriteLine($"[SSDIAG] Launch diagnostics: {LaunchDiagnostics.Summary}");

        if (LaunchDiagnostics.SafeLaunch)
            return new Window(CreateSafeLaunchPage());

        if (LaunchDiagnostics.MinimalShell)
        {
            Console.WriteLine("[SSDIAG] MinimalShell window: Shell + one plain page, no tabs, no Muninn pages");
            var shell = new Shell();
            shell.Items.Add(new ShellContent
            {
                Title = "Diagnostic",
                Route = "MinimalShellDiag",
                Content = CreateDiagnosticPage("Minimal Shell",
                    "Shell hosting a single plain page. No tabs, no Muninn pages or controls.")
            });
            return new Window(shell);
        }

        if (LaunchDiagnostics.MinimalTabs)
        {
            Console.WriteLine("[SSDIAG] MinimalTabs window: Shell + hidden 3-tab TabBar of plain pages");
            var shell = new Shell();
            var tabBar = new TabBar();
            for (var i = 1; i <= 3; i++)
            {
                tabBar.Items.Add(new ShellContent
                {
                    Title = $"Tab {i}",
                    Route = $"MinimalTabDiag{i}",
                    Content = CreateDiagnosticPage($"Minimal Tabs — Tab {i}",
                        "Shell TabBar structure mirroring AppShell (native tab bar hidden), plain pages only.")
                });
            }

            // Mirror AppShell.xaml: the native tab bar is hidden and Muninn draws its own.
            Shell.SetTabBarIsVisible(tabBar, false);
            shell.Items.Add(tabBar);
            return new Window(shell);
        }

        if (LaunchDiagnostics.NoShellInbox)
        {
            Console.WriteLine("[SSDIAG] NoShellInbox window: real InboxPage hosted WITHOUT Shell");
            var inbox = _services.GetRequiredService<Views.InboxPage>();
            return new Window(inbox);
        }

        return new Window(new AppShell());
    }

    protected override async void OnStart()
    {
        base.OnStart();
        if (DiagnosticWindowMode || LaunchDiagnostics.DisableStartupWork)
        {
            Console.WriteLine("[SSDIAG] App.OnStart skipped by launch diagnostics");
            return;
        }

        await CheckAuthAsync();
    }

    protected override async void OnResume()
    {
        base.OnResume();
        if (DiagnosticWindowMode || LaunchDiagnostics.DisableStartupWork)
        {
            Console.WriteLine("[SSDIAG] App.OnResume skipped by launch diagnostics");
            return;
        }

        if (await TryProcessPendingShareAsync())
            await ShowInboxAfterPendingShareAsync();

        await PublishScreenshotReviewCountAsync();
    }

    private static ContentPage CreateDiagnosticPage(string title, string detail)
    {
        return new ContentPage
        {
            BackgroundColor = Color.FromArgb("#FAFAF8"),
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(24),
                Spacing = 12,
                VerticalOptions = LayoutOptions.Center,
                Children =
                {
                    new Label
                    {
                        Text = title,
                        FontSize = 32,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#1C1C1E"),
                        HorizontalTextAlignment = TextAlignment.Center
                    },
                    new Label
                    {
                        Text = detail,
                        FontSize = 16,
                        TextColor = Color.FromArgb("#8E8E93"),
                        HorizontalTextAlignment = TextAlignment.Center
                    }
                }
            }
        };
    }

    private static ContentPage CreateSafeLaunchPage()
    {
        return new ContentPage
        {
            BackgroundColor = Color.FromArgb("#FAFAF8"),
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(24),
                Spacing = 12,
                VerticalOptions = LayoutOptions.Center,
                Children =
                {
                    new Label
                    {
                        Text = "Muninn Safe Launch",
                        FontSize = 32,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#1C1C1E"),
                        HorizontalTextAlignment = TextAlignment.Center
                    },
                    new Label
                    {
                        Text = "Shell, Inbox, Photos, and startup network work are bypassed for this diagnostic run.",
                        FontSize = 16,
                        TextColor = Color.FromArgb("#8E8E93"),
                        HorizontalTextAlignment = TextAlignment.Center
                    }
                }
            }
        };
    }

    private async Task CheckAuthAsync()
    {
        try
        {
            // Try SecureStorage first; fall back to Preferences (used when SecureStorage
            // is unavailable, e.g. Mac Catalyst running without a provisioning profile).
            string? token = null;
            try { token = await SecureStorage.GetAsync("auth_token"); } catch { }
            if (string.IsNullOrEmpty(token))
                token = Preferences.Get("auth_token_fallback", null as string);

            if (!string.IsNullOrEmpty(token) && !JwtHelper.IsExpiredOrInvalid(token))
            {
                // Valid, non-expired token — restore it, process any share-extension
                // pending URL, then navigate to the inbox.  Processing first means
                // the explicit Inbox refresh below will see the new save immediately.
                _apiService.SetAuthToken(token);
                if (await TryProcessPendingShareAsync())
                    await ShowInboxAfterPendingShareAsync();
                else if (await _screenshotReviewService.ShouldShowPrimingAutomaticallyAsync())
                    await Shell.Current.GoToAsync(nameof(ScreenshotPermissionPage));
                else
                    // Navigate AND force a refresh: Shell shows the first tab (Inbox)
                    // on launch before CheckAuthAsync sets the token, so its initial
                    // OnAppearing load fires unauthenticated and 403s into an empty
                    // inbox. Re-running RefreshAsync here — after SetAuthToken — reloads
                    // with the Bearer token attached so the saves actually appear.
                    await ShowInboxAfterPendingShareAsync();

                await PublishScreenshotReviewCountAsync();
            }
            else
            {
                // Missing, malformed, or expired — wipe storage and force re-auth.
                // No network call needed: exp is encoded in the JWT payload.
                ClearStoredToken();
                _apiService.ClearAuthToken();
                await Shell.Current.GoToAsync(nameof(AuthPage));
            }
        }
        catch
        {
            // SecureStorage can throw on first run on some platforms.
            await Shell.Current.GoToAsync(nameof(AuthPage));
        }
    }

    /// <summary>
    /// Reads any URL written by the iOS Share Extension and creates a save for it.
    /// Swallows all exceptions — a network failure leaves the pending key intact so the
    /// next session retries.  Must only be called after SetAuthToken has been invoked.
    /// </summary>
    private async Task<bool> TryProcessPendingShareAsync()
    {
        try { return await _apiService.ProcessPendingShareAsync(); }
        catch { return false; /* network failure — key preserved, will retry on next launch/login */ }
    }

    private static async Task ShowInboxAfterPendingShareAsync()
    {
        await Shell.Current.GoToAsync("//InboxPage");

        if (Shell.Current.CurrentPage is InboxPage inboxPage)
            await inboxPage.RefreshAsync();
    }

    private async Task PublishScreenshotReviewCountAsync()
    {
        try
        {
            var count = await _screenshotReviewService.GetUnreviewedCountAsync();
            WeakReferenceMessenger.Default.Send(new ScreenshotReviewCountChangedMessage(count));
        }
        catch { }
    }

    /// <summary>
    /// Removes the JWT from SecureStorage and the Preferences fallback.
    /// Called on cold-launch expiry and by <see cref="ApiService"/> on mid-session 401.
    ///
    /// We call both Remove("auth_token") and RemoveAll() because Remove can fail silently
    /// on the iOS simulator when no provisioning profile is present.  RemoveAll() wipes the
    /// entire SecureStorage namespace for this app, which is safe since auth_token is the
    /// only key we store there.
    /// </summary>
    internal static void ClearStoredToken()
    {
        try { SecureStorage.Default.Remove("auth_token"); } catch { }
        try { SecureStorage.Default.RemoveAll(); }         catch { }
        Preferences.Remove("auth_token_fallback");
    }
}
