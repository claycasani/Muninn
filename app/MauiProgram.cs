using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Muninn.Services;
using Muninn.ViewModels;
using Muninn.Views;
using Syncfusion.Licensing;
using Syncfusion.Maui.Core.Hosting;
using System.Reflection;
#if IOS
using Muninn.Platforms.iOS.Services;
#endif

namespace Muninn;

public static class MauiProgram
{
#if MACCATALYST
    // Used to call setFocusRingType: (NSFocusRingType.None = 1) on UIView subclasses.
    // ObjCRuntime.Messaging is internal in .NET 8+, so we call objc_msgSend directly.
    [System.Runtime.InteropServices.DllImport(ObjCRuntime.Constants.ObjectiveCLibrary,
        EntryPoint = "objc_msgSend")]
    static extern void SetFocusRingType(IntPtr receiver, IntPtr selector, nuint value);
#endif

    public static MauiApp CreateMauiApp()
    {
        // Build-identity breadcrumb: confirms WHICH build is actually running on the
        // device when streaming the console. If you reproduce a crash and do NOT see
        // this line, the device is running a stale binary, not this source.
        const string buildTag = "CATEGORIES-6";
        Console.WriteLine($"[SSDIAG] BUILD TAG: {buildTag}");
        System.Diagnostics.Debug.WriteLine($"[SSDIAG] BUILD TAG: {buildTag}");

        // Confirms the Liquid-Glass gate's runtime decision. Glass was exonerated by
        // the 2026-07 respring investigation (root cause: unsized-SVG bitmaps), so
        // the expected value is ENABLED; DISABLED means the MUNINN_DISABLE_GLASS
        // kill-switch is set.
        Console.WriteLine($"[SSDIAG] LiquidGlass Syncfusion surfaces: " +
            $"{(Muninn.Controls.LiquidGlassCompatibility.ShouldUseSyncfusionGlass ? "ENABLED" : "DISABLED (MUNINN_DISABLE_GLASS kill-switch)")}");

        var builder = MauiApp.CreateBuilder();

        // Load appsettings.json from embedded resources
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("Muninn.appsettings.json");
        if (stream is not null)
        {
            var config = new ConfigurationBuilder().AddJsonStream(stream).Build();
            builder.Configuration.AddConfiguration(config);
        }

        // Optional local override for development. This file lives outside the compiled
        // appsettings.json so the backend URL can be changed without editing repo config.
        // Example path on device/simulator: FileSystem.AppDataDirectory/appsettings.local.json
        var localSettingsPath = Path.Combine(FileSystem.Current.AppDataDirectory, "appsettings.local.json");
        if (File.Exists(localSettingsPath))
        {
            using var localStream = File.OpenRead(localSettingsPath);
            var localConfig = new ConfigurationBuilder().AddJsonStream(localStream).Build();
            builder.Configuration.AddConfiguration(localConfig);
        }

        var bundleLocalSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.local.json");
        if (File.Exists(bundleLocalSettingsPath))
        {
            using var bundleLocalStream = File.OpenRead(bundleLocalSettingsPath);
            var bundleLocalConfig = new ConfigurationBuilder().AddJsonStream(bundleLocalStream).Build();
            builder.Configuration.AddConfiguration(bundleLocalConfig);
        }

        var sfKey = builder.Configuration["Syncfusion:LicenseKey"];
        if (!string.IsNullOrEmpty(sfKey))
            SyncfusionLicenseProvider.RegisterLicense(sfKey);

        builder
            .UseMauiApp<App>()
            .ConfigureSyncfusionCore()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            })
            .ConfigureMauiHandlers(handlers =>
            {
                // Mac Catalyst: remove the UITextField native rounded-rect border and
                // suppress the system focus ring so our InputBorder widget is the only
                // visual border. Without this, a blue ring appears on focus and a gray
                // box appears around every Entry.
#if MACCATALYST
                // Pointer (hand) cursor on buttons
                Microsoft.Maui.Handlers.ButtonHandler.Mapper.AppendToMapping(
                    "MuninButtonPointer", (handler, _) =>
                {
                    try { handler.PlatformView.PointerInteractionEnabled = true; }
                    catch { }
                });

                Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping(
                    "MuninEntryNoBorder", (handler, _) =>
                {
                    try
                    {
                        // Remove the UITextField native rounded-rect border.
                        handler.PlatformView.BorderStyle = UIKit.UITextBorderStyle.None;
                        // Make background transparent so the InputBorder paints it.
                        handler.PlatformView.BackgroundColor = UIKit.UIColor.Clear;
                        // Suppress the Mac focus ring (NSFocusRingType.None = 1).
                        SetFocusRingType(handler.PlatformView.Handle,
                            ObjCRuntime.Selector.GetHandle("setFocusRingType:"), 1);
                    }
                    catch { /* degrade gracefully — cosmetic only */ }
                });
#endif
#if IOS
                // iOS: transparent background so InputBorder is the only visible container.
                // MAUI already sets BorderStyle=None on iOS by default; this ensures no
                // subtle UITextField background bleeds through at either edge of the field.
                // InputAccessoryView = null removes the blue circle "done" button that MAUI
                // injects above the keyboard via the input accessory view.
                Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping(
                    "MuninEntryNoBorderiOS", (handler, _) =>
                {
                    try
                    {
                        handler.PlatformView.BorderStyle = UIKit.UITextBorderStyle.None;
                        handler.PlatformView.BackgroundColor = UIKit.UIColor.Clear;
                        handler.PlatformView.InputAccessoryView = null;
                    }
                    catch { }
                });

                // iOS 27 respring isolation: .NET MAUI 10 defaults CollectionView to
                // the new UICollectionView-based CollectionViewHandler2, which is the
                // prime suspect for the backboardd compositor blowup on the iOS 27
                // beta (constructing the Inbox CollectionView alone resprings the
                // device). This flag reverts every CollectionView to the legacy
                // handler so the hypothesis — and the fix — can be tested in one run.
                if (Muninn.Diagnostics.LaunchDiagnostics.LegacyCollectionHandler)
                {
                    handlers.AddHandler<CollectionView,
                        Microsoft.Maui.Controls.Handlers.Items.CollectionViewHandler>();
                    Console.WriteLine("[SSDIAG] CollectionView handler: LEGACY CollectionViewHandler (CV1) registered");
                }
#endif
            });

        // App (needs IApiService injected for auth gate)
        builder.Services.AddSingleton<App>();

        // Services
        builder.Services.AddSingleton<IApiService, ApiService>();
#if IOS
        builder.Services.AddSingleton<IScreenshotReviewService, IosScreenshotReviewService>();
#else
        builder.Services.AddSingleton<IScreenshotReviewService, ScreenshotReviewService>();
#endif

        // ViewModels
        builder.Services.AddTransient<AuthViewModel>();
        builder.Services.AddTransient<InboxViewModel>();
        builder.Services.AddTransient<ScreenshotPermissionViewModel>();
        builder.Services.AddTransient<ScreenshotsReviewViewModel>();
        builder.Services.AddTransient<SaveDetailViewModel>();
        builder.Services.AddTransient<DigestViewModel>();
        builder.Services.AddTransient<ArchiveViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddTransient<ManageCategoriesViewModel>();
        builder.Services.AddTransient<AccountViewModel>();
        builder.Services.AddTransient<ChangePasswordViewModel>();
        builder.Services.AddTransient<DeleteAccountViewModel>();

        // Views
        builder.Services.AddTransient<AuthPage>();
        builder.Services.AddTransient<InboxPage>();
        builder.Services.AddTransient<ScreenshotPermissionPage>();
        builder.Services.AddTransient<ScreenshotsReviewPage>();
        builder.Services.AddTransient<SaveDetailPage>();
        builder.Services.AddTransient<DigestPage>();
        builder.Services.AddTransient<ArchivePage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<ManageCategoriesPage>();
        builder.Services.AddTransient<AccountPage>();
        builder.Services.AddTransient<ChangePasswordPage>();
        builder.Services.AddTransient<DeleteAccountPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
