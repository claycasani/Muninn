namespace Muninn.Controls;

/// <summary>
/// Custom floating liquid-glass tab bar. Replaces Shell's native tab bar (which
/// reserves layout space and won't let page content scroll under it). This is an
/// overlay the three tab pages drop in at the bottom; page content fills full height
/// behind it, so cards refract through the Syncfusion glass pill like the iOS 26
/// Music app. Tab switching is routed through Shell's absolute routes.
/// Each page sets <see cref="Current"/> to highlight its own tab.
/// </summary>
public partial class GlassTabBar : ContentView
{
    private static readonly Color Olive = Color.FromArgb("#444A2E");
    private static readonly Color OffBlack = Color.FromArgb("#080807");

    public static readonly BindableProperty CurrentProperty =
        BindableProperty.Create(
            nameof(Current),
            typeof(string),
            typeof(GlassTabBar),
            string.Empty,
            propertyChanged: OnCurrentChanged);

    public string Current
    {
        get => (string)GetValue(CurrentProperty);
        set => SetValue(CurrentProperty, value);
    }

    public bool IsInboxSelected   => string.Equals(Current, "Inbox",   StringComparison.OrdinalIgnoreCase);
    public bool IsDigestSelected  => string.Equals(Current, "Digest",  StringComparison.OrdinalIgnoreCase);
    public bool IsArchiveSelected => string.Equals(Current, "Archive", StringComparison.OrdinalIgnoreCase);

    public Color InboxTint   => IsInboxSelected   ? Olive : OffBlack;
    public Color DigestTint  => IsDigestSelected  ? Olive : OffBlack;
    public Color ArchiveTint => IsArchiveSelected ? Olive : OffBlack;

    public GlassTabBar()
    {
        InitializeComponent();

        if (LiquidGlassCompatibility.ShouldUseSyncfusionGlass)
        {
            SurfaceHost.Remove(FallbackSurface);
            Console.WriteLine("[SSDIAG] GlassTabBar: Syncfusion Liquid Glass pill ACTIVE");
        }
        else
        {
            SurfaceHost.Remove(GlassSurface);
            SurfaceHost.Remove(GlassRim);
            Console.WriteLine("[SSDIAG] GlassTabBar: plain fallback pill (glass disabled by kill-switch)");
        }
    }

    private static void OnCurrentChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not GlassTabBar bar)
            return;

        bar.OnPropertyChanged(nameof(IsInboxSelected));
        bar.OnPropertyChanged(nameof(IsDigestSelected));
        bar.OnPropertyChanged(nameof(IsArchiveSelected));
        bar.OnPropertyChanged(nameof(InboxTint));
        bar.OnPropertyChanged(nameof(DigestTint));
        bar.OnPropertyChanged(nameof(ArchiveTint));
    }

    private async void OnInbox(object? sender, TappedEventArgs e)
    {
        if (!IsInboxSelected)
            await Shell.Current.GoToAsync("//InboxPage");
    }

    private async void OnDigest(object? sender, TappedEventArgs e)
    {
        if (!IsDigestSelected)
            await Shell.Current.GoToAsync("//DigestPage");
    }

    private async void OnArchive(object? sender, TappedEventArgs e)
    {
        if (!IsArchiveSelected)
            await Shell.Current.GoToAsync("//ArchivePage");
    }
}
