using Muninn.Views;
using Muninn.Diagnostics;

namespace Muninn;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Detail/modal routes — pushed onto the stack, not tabs.
        Routing.RegisterRoute(nameof(AuthPage), typeof(AuthPage));
        Routing.RegisterRoute(nameof(ScreenshotPermissionPage), typeof(ScreenshotPermissionPage));
        Routing.RegisterRoute(nameof(ScreenshotsReviewPage), typeof(ScreenshotsReviewPage));
        Routing.RegisterRoute(nameof(SaveDetailPage), typeof(SaveDetailPage));
        Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));
        Routing.RegisterRoute(nameof(ManageCategoriesPage), typeof(ManageCategoriesPage));
        Routing.RegisterRoute(nameof(AccountPage), typeof(AccountPage));
        Routing.RegisterRoute(nameof(ChangePasswordPage), typeof(ChangePasswordPage));
        Routing.RegisterRoute(nameof(DeleteAccountPage), typeof(DeleteAccountPage));

        // iOS / Mac Catalyst: safe area. Tab pages additionally pull their
        // CollectionView into the bottom safe-area inset (a negative bottom margin)
        // so card content reaches the true screen bottom and refracts through the
        // floating glass tab bar instead of leaving a cream safe-area band.
        Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific.Page
            .SetUseSafeArea(this, true);

#if IOS
        // Re-assert iOS chrome on EVERY navigation (persistent, not one-shot). iOS 26
        // Liquid Glass and MAUI's Shell renderer reset the navigation-bar appearance
        // after our pass — and again on each tab switch — so a single application at
        // startup does not hold. Navigated also fires *after* the Shell renderer writes
        // the UITabBarItem images, so our SF Symbol overrides won't be clobbered.
        if (LaunchDiagnostics.DisableIosChromeWork)
            Console.WriteLine("[SSDIAG] AppShell iOS chrome work disabled by launch diagnostics");
        else
            Navigated += OnNavigated;
#endif
    }

#if IOS
    private static bool loggedNativeChromeDecision;

    // Re-applied on every navigation. MAUI's Shell renderer configures the page's
    // navigation-bar appearance during the navigation pass, sometimes slightly after
    // our handler runs — so we re-assert immediately AND on two short delays to land
    // after MAUI's setup. All passes are idempotent.
    private void OnNavigated(object? sender, ShellNavigatedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(ApplyiOSChrome);
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(120), ApplyiOSChrome);
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(450), ApplyiOSChrome);
    }

    /// <summary>
    /// Re-asserts our iOS shell chrome. Idempotent and safe to call on every navigation.
    /// Walks the UITabBarController that MAUI's Shell renderer created and:
    ///   1. Applies a solid-cream (#FAFAF8) UINavigationBarAppearance on the large-title,
    ///      compact, and scrolled states — with the Liquid Glass material removed — so the
    ///      nav bar matches the page body with no white-vs-cream seam.
    ///   2. Moves the large title to the 24pt content gutter so it left-aligns with the
    ///      page content (ScreenMargin), instead of the iOS-default 16pt inset.
    ///   3. Enables PrefersLargeTitles on each tab's UINavigationController.
    ///   4. Sets SF Symbol images on each UITabBarItem. This is more reliable than
    ///      MauiImage SVGs on the iOS 18+ Liquid Glass tab bar.
    /// (Large-title *visibility* per page is driven by ios:Page.LargeTitleDisplay in XAML,
    ///  so detail pages that opt out are not forced into large titles here.)
    /// </summary>
    private static void ApplyiOSChrome()
    {
        try
        {
            // Resolve the root view controller via ConnectedScenes (iOS 13+ API).
            var scene = UIKit.UIApplication.SharedApplication.ConnectedScenes
                .OfType<UIKit.UIWindowScene>()
                .FirstOrDefault();
            var keyWindow = scene?.Windows.FirstOrDefault(w => w.IsKeyWindow);
            var rootVC = keyWindow?.RootViewController
#pragma warning disable CA1422
                      ?? UIKit.UIApplication.SharedApplication.KeyWindow?.RootViewController;
#pragma warning restore CA1422

            if (rootVC is null) return;

            // ── Window background ────────────────────────────────────────────────────
            // Cream window so any transparent nav-bar region shows cream, not white.
            var bgColor  = UIKit.UIColor.FromRGB(0xFA, 0xFA, 0xF8);
            var sepColor = UIKit.UIColor.FromRGB(0xE5, 0xE5, 0xEA);

            if (keyWindow != null)
            {
                keyWindow.BackgroundColor = bgColor;
                keyWindow.OverrideUserInterfaceStyle = UIKit.UIUserInterfaceStyle.Light;
            }

            // MAUI Shell wraps its UITabBarController inside a container VC —
            // it is NOT the direct root. Walk the tree to find it.
            var tabVC = FindTabBarController(rootVC);
            if (tabVC is null) return;
            var useNativeLiquidGlassChrome =
                OperatingSystem.IsIOSVersionAtLeast(26) &&
                !OperatingSystem.IsIOSVersionAtLeast(27);

            if (!loggedNativeChromeDecision)
            {
                loggedNativeChromeDecision = true;
                var message = useNativeLiquidGlassChrome
                    ? "[SSDIAG] AppShell native UITabBar glass: ENABLED (iOS 26 path)"
                    : OperatingSystem.IsIOSVersionAtLeast(27)
                        ? "[SSDIAG] AppShell native UITabBar glass: DISABLED (iOS 27 fallback)"
                        : "[SSDIAG] AppShell native UITabBar glass: not applicable";
                Console.WriteLine(message);
                System.Diagnostics.Debug.WriteLine(message);
            }

            // ── Tab bar glass + tint (iOS 26) ────────────────────────────────────────
            // ConfigureWithDefaultBackground() restores the iOS 26 floating glass pill.
            // After calling it, we override the selected-item color to olive (#444A2E)
            // because ConfigureWithDefaultBackground() resets it to system blue.
            // Unselected items stay at the gray defined by Shell.TabBarUnselectedColor.
            // TintColor is also set as a fallback for SF Symbol rendering.
            //
            // Shell.TabBarBackgroundColor="Transparent" in AppShell.xaml removes the
            // cream fill MAUI injects; this appearance block then owns all tab bar visuals.
            if (useNativeLiquidGlassChrome)
            {
                var olive = UIKit.UIColor.FromRGB(0x44, 0x4A, 0x2E);
                var gray  = UIKit.UIColor.FromRGB(0x8E, 0x8E, 0x93);

                var glassTabBar = new UIKit.UITabBarAppearance();
                glassTabBar.ConfigureWithDefaultBackground();

                // Selected state: olive icon + olive label
                glassTabBar.StackedLayoutAppearance.Selected.IconColor = olive;
                glassTabBar.StackedLayoutAppearance.Selected.TitleTextAttributes =
                    new UIKit.UIStringAttributes { ForegroundColor = olive };
                glassTabBar.InlineLayoutAppearance.Selected.IconColor = olive;
                glassTabBar.InlineLayoutAppearance.Selected.TitleTextAttributes =
                    new UIKit.UIStringAttributes { ForegroundColor = olive };
                glassTabBar.CompactInlineLayoutAppearance.Selected.IconColor = olive;
                glassTabBar.CompactInlineLayoutAppearance.Selected.TitleTextAttributes =
                    new UIKit.UIStringAttributes { ForegroundColor = olive };

                // Unselected state: secondary gray
                glassTabBar.StackedLayoutAppearance.Normal.IconColor = gray;
                glassTabBar.StackedLayoutAppearance.Normal.TitleTextAttributes =
                    new UIKit.UIStringAttributes { ForegroundColor = gray };
                glassTabBar.InlineLayoutAppearance.Normal.IconColor = gray;
                glassTabBar.InlineLayoutAppearance.Normal.TitleTextAttributes =
                    new UIKit.UIStringAttributes { ForegroundColor = gray };
                glassTabBar.CompactInlineLayoutAppearance.Normal.IconColor = gray;
                glassTabBar.CompactInlineLayoutAppearance.Normal.TitleTextAttributes =
                    new UIKit.UIStringAttributes { ForegroundColor = gray };

                tabVC.TabBar.StandardAppearance   = glassTabBar;
                tabVC.TabBar.ScrollEdgeAppearance = glassTabBar;
                tabVC.TabBar.TintColor            = olive;
                tabVC.TabBar.BackgroundColor      = UIKit.UIColor.Clear;
                tabVC.TabBar.BarTintColor         = UIKit.UIColor.Clear;
                tabVC.TabBar.Opaque               = false;
                tabVC.TabBar.Translucent          = true;
                tabVC.TabBar.ClipsToBounds        = false;

                // MAUI injects a UIView below the tab bar (in the home-indicator safe
                // area) and fills it with Shell.TabBarBackgroundColor. Even with
                // TabBarBackgroundColor="Transparent" MAUI may still add this fill view
                // with a non-clear background, producing a solid cream rectangle behind
                // the floating pill. Walk tabVC.View.Subviews and clear any fill view
                // whose top edge is at or below the tab bar — the pill itself is
                // excluded because it IS the UITabBar.
                if (tabVC.View != null)
                {
                    nfloat tabMinY = tabVC.TabBar.Frame.Y;
                    ClearTabBarFooterFillViews(tabVC.View, tabVC.TabBar, tabMinY);
                }
            }
            else if (OperatingSystem.IsIOSVersionAtLeast(27))
            {
                // iOS 27 beta compatibility path: Shell's native UITabBar is hidden
                // and Muninn draws its own lightweight tab bar. Do not ask UIKit for
                // the iOS 26+ default glass material here; paired with the custom MAUI
                // overlay it can still create compositor work even when Shell says the
                // tab bar is invisible.
                var plainTabBar = new UIKit.UITabBarAppearance();
                plainTabBar.ConfigureWithTransparentBackground();
                plainTabBar.BackgroundColor = UIKit.UIColor.Clear;
                plainTabBar.BackgroundEffect = null;
                plainTabBar.ShadowColor = UIKit.UIColor.Clear;

                tabVC.TabBar.StandardAppearance = plainTabBar;
                tabVC.TabBar.ScrollEdgeAppearance = plainTabBar;
                tabVC.TabBar.BackgroundColor = UIKit.UIColor.Clear;
                tabVC.TabBar.BarTintColor = UIKit.UIColor.Clear;
                tabVC.TabBar.Opaque = false;
                tabVC.TabBar.Translucent = true;
                tabVC.TabBar.ClipsToBounds = false;
                tabVC.TabBar.Hidden = true;
            }

            // ── Navigation bar appearance ────────────────────────────────────────────
            // ConfigureWithOpaqueBackground() installs a chrome *material* (the iOS 26
            // Liquid Glass blur) that renders white over our cream regardless of
            // BackgroundColor. Nulling BackgroundEffect removes that material and leaves
            // only the solid BackgroundColor — so cream actually shows through. We apply
            // this to ALL appearance slots so the colour holds in the large-title state
            // and after the user scrolls into the compact state.
            UIKit.UINavigationBarAppearance CreamAppearance(UIKit.UIColor shadow)
            {
                var a = new UIKit.UINavigationBarAppearance();
                a.ConfigureWithOpaqueBackground();
                a.BackgroundColor  = bgColor;
                a.BackgroundEffect = null;   // strip the Liquid Glass material
                a.ShadowColor      = shadow;
                return a;
            }

            // Large-title (scroll-edge) state: no hairline. Compact/scrolled state: hairline.
            var edgeAppearance     = CreamAppearance(UIKit.UIColor.Clear);
            var standardAppearance = CreamAppearance(sepColor);

            // On iOS 26, keep the tab controller itself transparent so the floating
            // system glass tab bar can refract live page content instead of a solid
            // Shell-painted footer. The window remains cream, so blank page regions
            // still look like the Muninn background.
            if (tabVC.View != null)
            {
                tabVC.View.BackgroundColor = useNativeLiquidGlassChrome
                    ? UIKit.UIColor.Clear
                    : bgColor;
                tabVC.View.Opaque = !useNativeLiquidGlassChrome;
            }

            // Find the REAL UINavigationControllers. In MAUI Shell they are nested inside
            // per-section container VCs — they are NOT direct children of the tab bar
            // controller — so iterating `tabVC.ViewControllers` with an `is
            // UINavigationController` check matches nothing and the appearance is never
            // applied. Walk the whole VC tree to collect them instead.
            var navControllers = new List<UIKit.UINavigationController>();
            CollectNavigationControllers(rootVC, navControllers);

            foreach (var navVC in navControllers)
            {
                if (navVC.View != null)
                    navVC.View.BackgroundColor = bgColor;

                var bar = navVC.NavigationBar;
                bar.PrefersLargeTitles          = true;
                bar.ScrollEdgeAppearance        = edgeAppearance;
                bar.CompactScrollEdgeAppearance = edgeAppearance;
                bar.StandardAppearance          = standardAppearance;
                bar.CompactAppearance           = standardAppearance;
                bar.BackgroundColor             = bgColor;

                // Align the large title with the 24pt content gutter (ScreenMargin)
                // rather than the iOS-default 16pt, so the title shares one left edge
                // with the page content (paste-a-link input + save cards).
                bar.DirectionalLayoutMargins = new UIKit.NSDirectionalEdgeInsets(0, 24, 0, 24);

                // MAUI also sets appearance on the per-page UINavigationItem, which takes
                // precedence over the bar-level appearance — re-assert there too so cream wins.
                if (navVC.TopViewController?.NavigationItem is { } navItem)
                {
                    navItem.ScrollEdgeAppearance = edgeAppearance;
                    navItem.StandardAppearance   = standardAppearance;
                    navItem.CompactAppearance    = standardAppearance;
                }

                // ── Extend page content under the floating glass tab bar ─────────────
                // By default MAUI/UIKit insets each page's content so it stops at the
                // tab bar's top edge, leaving a flat cream band where the floating pill
                // sits. To get the iOS-26 look (content scrolls *under* the translucent
                // pill, refracting through the glass), each page view controller must
                // extend its layout under the bottom bar. EdgesForExtendedLayout=Bottom
                // + a translucent tab bar makes UIKit lay the content out full-height and
                // supply the bottom scroll inset for reachability. Re-applied every pass
                // because MAUI resets it during its own layout.
                foreach (var pageVC in navVC.ViewControllers)
                {
                    pageVC.EdgesForExtendedLayout        = UIKit.UIRectEdge.Bottom;
                    pageVC.ExtendedLayoutIncludesOpaqueBars = true;
                    // Drop any bottom inset MAUI added for the tab bar; the translucent
                    // bar's own safe area still keeps the last items reachable.
                    var ai = pageVC.AdditionalSafeAreaInsets;
                    if (ai.Bottom != 0)
                        pageVC.AdditionalSafeAreaInsets =
                            new UIKit.UIEdgeInsets(ai.Top, ai.Left, 0, ai.Right);
                }
            }

            // ── SF Symbol tab icons ──────────────────────────────────────────────────
            // Ordered to match ShellContent declarations in AppShell.xaml.
            // Use symbols that have a clear outline ↔ fill distinction so the
            // selected vs unselected state is visually obvious. Avoid symbols like
            // "sparkles" that look the same in both weights.
            (string normal, string selected)[] icons =
            [
                ("tray",       "tray.fill"),           // Inbox
                ("newspaper",  "newspaper.fill"),       // Digest — clear outline/fill pair
                ("archivebox", "archivebox.fill"),      // Archive
            ];

            var items = tabVC.TabBar?.Items;
            if (items is null || OperatingSystem.IsIOSVersionAtLeast(27)) return;

            for (int i = 0; i < Math.Min(items.Length, icons.Length); i++)
            {
                items[i].Image         = UIKit.UIImage.GetSystemImage(icons[i].normal);
                items[i].SelectedImage = UIKit.UIImage.GetSystemImage(icons[i].selected);
            }
        }
        catch { /* cosmetic — never crash the app */ }
    }

    /// <summary>
    /// Depth-first walk collecting every UINavigationController in the VC tree. MAUI Shell
    /// nests these inside per-section container VCs, so they are not reachable as direct
    /// children of the tab bar controller.
    /// </summary>
    private static void CollectNavigationControllers(
        UIKit.UIViewController vc, List<UIKit.UINavigationController> acc)
    {
        if (vc is UIKit.UINavigationController nav && !acc.Contains(nav))
            acc.Add(nav);

        if (vc.PresentedViewController is { } presented)
            CollectNavigationControllers(presented, acc);

        foreach (var child in vc.ChildViewControllers)
            CollectNavigationControllers(child, acc);
    }

    /// <summary>
    /// Clears MAUI/Shell filler views that sit in the tab-bar safe-area region.
    /// These can be direct siblings or nested container descendants depending on
    /// the Shell renderer pass, so this walks recursively below the tab-bar Y.
    /// </summary>
    private static void ClearTabBarFooterFillViews(
        UIKit.UIView view,
        UIKit.UITabBar tabBar,
        nfloat tabMinY)
    {
        foreach (var sub in view.Subviews)
        {
            if (sub == tabBar)
                continue;

            var frameInTabRoot = sub.Superview == view
                ? sub.Frame
                : view.ConvertRectFromView(sub.Frame, sub.Superview);

            if (frameInTabRoot.Y >= tabMinY)
            {
                sub.BackgroundColor = UIKit.UIColor.Clear;
                sub.Opaque = false;
            }

            ClearTabBarFooterFillViews(sub, tabBar, tabMinY);
        }
    }

    /// <summary>
    /// Depth-first walk of the VC tree to locate the UITabBarController that
    /// MAUI's Shell renderer embeds inside a container view controller.
    /// </summary>
    private static UIKit.UITabBarController? FindTabBarController(UIKit.UIViewController vc)
    {
        if (vc is UIKit.UITabBarController tbc) return tbc;

        // Check VCs presented modally on top of this one.
        if (vc.PresentedViewController is { } presented)
        {
            var found = FindTabBarController(presented);
            if (found is not null) return found;
        }

        // Check child container VCs (MAUI uses these heavily).
        foreach (var child in vc.ChildViewControllers)
        {
            var found = FindTabBarController(child);
            if (found is not null) return found;
        }

        return null;
    }
#endif

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

#if MACCATALYST
        // Mac Catalyst has a single root navigation controller rather than per-tab ones;
        // walk up from the Shell's platform view to find it.
        try
        {
            if (Handler?.PlatformView is UIKit.UIView view)
            {
                var vc = view.NextResponder as UIKit.UIViewController
#pragma warning disable CA1422
                      ?? UIKit.UIApplication.SharedApplication.KeyWindow?.RootViewController;
#pragma warning restore CA1422
                if (vc?.NavigationController?.NavigationBar is { } navBar)
                    navBar.PrefersLargeTitles = true;
            }
        }
        catch { }
#endif
    }
}
