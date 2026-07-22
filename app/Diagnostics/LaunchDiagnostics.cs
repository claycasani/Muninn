namespace Muninn.Diagnostics;

internal static class LaunchDiagnostics
{
    public static bool SafeLaunch => IsEnabled("MUNINN_SAFE_LAUNCH");
    public static bool DisableStartupWork => IsEnabled("MUNINN_DISABLE_STARTUP_WORK");
    public static bool DisableInboxRefresh => IsEnabled("MUNINN_DISABLE_INBOX_REFRESH");
    public static bool HideInboxChrome => IsEnabled("MUNINN_HIDE_INBOX_CHROME");
    public static bool DisableIosChromeWork => IsEnabled("MUNINN_DISABLE_IOS_CHROME_WORK");

    // Photos-vs-Inbox-render isolation. When set, granting Photos access requests
    // authorization and captures the baseline as usual, but does NOT navigate to
    // //InboxPage afterward — the user stays on the (already-rendered) permission
    // page. If the device still resprings on grant with this on, the Photos grant
    // path itself is implicated; if it survives, the first Inbox render is the
    // trigger and Photos is innocent.
    public static bool GrantNoInboxNav => IsEnabled("MUNINN_GRANT_NO_INBOX_NAV");

    // Inbox render-surface isolation for the iOS 27 respring. When set, InboxPage
    // uses a conventional layout: SafeAreaEdges back to default (not edge-to-edge)
    // and the CollectionView's negative bottom margin (-44, which pulls its frame
    // into the bottom safe-area inset) reset to 0. If the respring stops with this
    // on, the edge-to-edge/negative-margin compositor path is the trigger.
    public static bool InboxStdLayout => IsEnabled("MUNINN_INBOX_STD_LAYOUT");

    // Shell-vs-page triangulation for the iOS 27 respring. Safe launch (bare
    // ContentPage, no Shell) survives while the full AppShell dies even with every
    // Muninn behavior disabled — these three modes split the layers in between:
    //   MinimalShell — Shell hosting ONE plain ContentPage. No tabs, no Muninn pages.
    //   MinimalTabs  — Shell with a hidden 3-tab TabBar of plain pages (mirrors
    //                  AppShell's TabBarIsVisible=False structure, no Muninn pages).
    //   NoShellInbox — the real InboxPage hosted directly in the Window, no Shell.
    public static bool MinimalShell => IsEnabled("MUNINN_MINIMAL_SHELL");
    public static bool MinimalTabs => IsEnabled("MUNINN_MINIMAL_TABS");
    public static bool NoShellInbox => IsEnabled("MUNINN_NO_SHELL_INBOX");

    // InboxPage-internal split (combine with NoShellInbox for the fastest repro).
    // These REMOVE subtrees from the page's root grid before it is hosted — unlike
    // IsVisible=false (HideInboxChrome), removal prevents native view/handler
    // creation entirely, which is what matters for a compositor-level failure.
    //   InboxNoCollection — remove the CollectionView (and with it its whole header:
    //                       title, link bar, search bar, category chips, cards).
    //   InboxNoChromeTree — remove TopChromeBar (3 ChromeGlassButtons) and
    //                       BottomChromeBar (GlassTabBar) from the tree.
    public static bool InboxNoCollection => IsEnabled("MUNINN_INBOX_NO_COLLECTION");
    public static bool InboxNoChromeTree => IsEnabled("MUNINN_INBOX_NO_CHROME_TREE");

    // CollectionView-handler isolation. Run 10 (real CollectionView present but
    // IsVisible=false behind the loading spinner) resprang, so constructing the
    // native CollectionView view is enough. .NET MAUI 10 made the new
    // UICollectionView-based CollectionViewHandler2 the iOS default — prime suspect
    // against the iOS 27 beta compositor.
    //   LegacyCollectionHandler — register MAUI's legacy CollectionViewHandler (CV1)
    //                             for every CollectionView. Fix candidate: run the
    //                             FULL app with only this flag.
    //   InboxBareCollection     — (with NoShellInbox + InboxNoCollection) add a bare
    //                             code-built CollectionView of 40 strings, default
    //                             template, no header — isolates the control from
    //                             Muninn's templates/header content.
    public static bool LegacyCollectionHandler => IsEnabled("MUNINN_LEGACY_CV_HANDLER");
    public static bool InboxBareCollection => IsEnabled("MUNINN_INBOX_BARE_COLLECTION");

    // CollectionView.Header split. The legacy-handler run (Run 11) still resprang, so
    // this is not CV2-specific — suspicion moves to the header content, which is
    // constructed even when the list is invisible and empty (matching every crash).
    //   InboxNoCvHeader — null out the entire CollectionView.Header.
    //   InboxNoChips    — remove only the horizontal ScrollView of category chips
    //                     (a nested scrollable inside a CV header: layout-loop risk).
    //   InboxNoSearch   — remove only the SearchBarView.
    //   InboxNoLinkBar  — remove only the collapsed link-entry bar (zero-height grid).
    public static bool InboxNoCvHeader => IsEnabled("MUNINN_INBOX_NO_CV_HEADER");
    public static bool InboxNoChips => IsEnabled("MUNINN_INBOX_NO_CHIPS");
    public static bool InboxNoSearch => IsEnabled("MUNINN_INBOX_NO_SEARCH");
    public static bool InboxNoLinkBar => IsEnabled("MUNINN_INBOX_NO_LINKBAR");

    // Kill-switch for the restored Syncfusion Liquid Glass chrome (glass was
    // exonerated by the respring investigation; this exists so any future
    // glass-suspicion can be tested with one env var instead of a code change).
    public static bool DisableGlass => IsEnabled("MUNINN_DISABLE_GLASS");

    public static string Summary =>
        $"SafeLaunch={SafeLaunch}, DisableStartupWork={DisableStartupWork}, " +
        $"DisableInboxRefresh={DisableInboxRefresh}, HideInboxChrome={HideInboxChrome}, " +
        $"DisableIosChromeWork={DisableIosChromeWork}, GrantNoInboxNav={GrantNoInboxNav}, " +
        $"InboxStdLayout={InboxStdLayout}, MinimalShell={MinimalShell}, " +
        $"MinimalTabs={MinimalTabs}, NoShellInbox={NoShellInbox}, " +
        $"InboxNoCollection={InboxNoCollection}, InboxNoChromeTree={InboxNoChromeTree}, " +
        $"LegacyCollectionHandler={LegacyCollectionHandler}, InboxBareCollection={InboxBareCollection}, " +
        $"InboxNoCvHeader={InboxNoCvHeader}, InboxNoChips={InboxNoChips}, " +
        $"InboxNoSearch={InboxNoSearch}, InboxNoLinkBar={InboxNoLinkBar}";

    private static bool IsEnabled(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (IsTruthy(value))
            return true;

        var switchName = $"--{name.ToLowerInvariant().Replace('_', '-')}";
        return Environment.GetCommandLineArgs()
            .Any(arg => string.Equals(arg, switchName, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsTruthy(string? value) =>
        value is not null &&
        (value.Equals("1", StringComparison.OrdinalIgnoreCase) ||
         value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
         value.Equals("yes", StringComparison.OrdinalIgnoreCase));
}
