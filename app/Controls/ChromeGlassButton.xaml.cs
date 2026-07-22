namespace Muninn.Controls;

public partial class ChromeGlassButton : ContentView
{
    // Press-brighten tints: rest matches the XAML glass tint; pressed is a strong
    // white wash. Animated on the ACTIVE surface's BackgroundColor because that
    // property provably renders on-device (the overlay-only approach never showed).
    private static readonly Color GlassRestTint = Color.FromArgb("#3DF7F7F2");
    private static readonly Color GlassPressedTint = Color.FromArgb("#B0FFFFFF");

    private VisualElement? _pressSurface;
    private Color? _pressSurfaceRestTint;
    private bool _isClickDispatching;

    public event EventHandler? Clicked;

    public static readonly BindableProperty IconSourceProperty =
        BindableProperty.Create(
            nameof(IconSource),
            typeof(string),
            typeof(ChromeGlassButton),
            null,
            propertyChanged: OnIconSourceChanged);

    public static readonly BindableProperty IconTextProperty =
        BindableProperty.Create(
            nameof(IconText),
            typeof(string),
            typeof(ChromeGlassButton),
            null,
            propertyChanged: OnIconTextChanged);

    public static readonly BindableProperty IconColorProperty =
        BindableProperty.Create(
            nameof(IconColor),
            typeof(Color),
            typeof(ChromeGlassButton),
            Color.FromArgb("#1A1A1A"));

    public static readonly BindableProperty BadgeTextProperty =
        BindableProperty.Create(nameof(BadgeText), typeof(string), typeof(ChromeGlassButton), null);

    public static readonly BindableProperty BadgeVisibleProperty =
        BindableProperty.Create(nameof(BadgeVisible), typeof(bool), typeof(ChromeGlassButton), false);

    public static readonly BindableProperty ButtonSizeProperty =
        BindableProperty.Create(
            nameof(ButtonSize),
            typeof(double),
            typeof(ChromeGlassButton),
            50d,
            propertyChanged: OnButtonSizeChanged);

    public static readonly BindableProperty IconSizeProperty =
        BindableProperty.Create(nameof(IconSize), typeof(double), typeof(ChromeGlassButton), 22d);

    public static readonly BindableProperty IconTextSizeProperty =
        BindableProperty.Create(nameof(IconTextSize), typeof(double), typeof(ChromeGlassButton), 28d);

    public static readonly BindableProperty CornerRadiusProperty =
        BindableProperty.Create(nameof(CornerRadius), typeof(double), typeof(ChromeGlassButton), 25d);

    // Optical-centering nudge for asymmetric glyphs (e.g. the "<" back chevron reads
    // off-center when geometrically centered). Applied as TranslationX on the icon.
    public static readonly BindableProperty IconTranslationXProperty =
        BindableProperty.Create(nameof(IconTranslationX), typeof(double), typeof(ChromeGlassButton), 0d);

    public ChromeGlassButton()
    {
        InitializeComponent();

        if (LiquidGlassCompatibility.ShouldUseSyncfusionGlass)
        {
            // Genuine Liquid Glass: drop the plain fallback surface so the
            // Syncfusion glass material is the button's only background.
            SurfaceHost.Remove(FallbackSurface);
            _pressSurface = GlassSurface;
            _pressSurfaceRestTint = GlassRestTint;
            Console.WriteLine("[SSDIAG] ChromeGlassButton: Syncfusion Liquid Glass surface ACTIVE");
        }
        else
        {
            // Kill-switch path (MUNINN_DISABLE_GLASS): drop the glass surface (and
            // its specular rim) and neutralize the ripple/highlight; EffectsHost
            // stays only as the touch source so Clicked keeps working identically.
            SurfaceHost.Remove(GlassSurface);
            // Rest tint resolved lazily on first press (style applies the color later).
            _pressSurface = FallbackSurface;
            // Fallback surface carries its own stroke; drop the lens-edge hairline.
            VisualFrame.StrokeThickness = 0;
            EffectsHost.TouchDownEffects = Syncfusion.Maui.Core.SfEffects.None;
            EffectsHost.TouchUpEffects = Syncfusion.Maui.Core.SfEffects.None;
            Console.WriteLine("[SSDIAG] ChromeGlassButton: plain fallback chrome (glass disabled by kill-switch)");
        }
    }

    public string? IconSource
    {
        get => (string?)GetValue(IconSourceProperty);
        set => SetValue(IconSourceProperty, value);
    }

    public string? IconText
    {
        get => (string?)GetValue(IconTextProperty);
        set => SetValue(IconTextProperty, value);
    }

    public Color IconColor
    {
        get => (Color)GetValue(IconColorProperty);
        set => SetValue(IconColorProperty, value);
    }

    public string? BadgeText
    {
        get => (string?)GetValue(BadgeTextProperty);
        set => SetValue(BadgeTextProperty, value);
    }

    public bool BadgeVisible
    {
        get => (bool)GetValue(BadgeVisibleProperty);
        set => SetValue(BadgeVisibleProperty, value);
    }

    public double ButtonSize
    {
        get => (double)GetValue(ButtonSizeProperty);
        set => SetValue(ButtonSizeProperty, value);
    }

    public double IconSize
    {
        get => (double)GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    public double IconTextSize
    {
        get => (double)GetValue(IconTextSizeProperty);
        set => SetValue(IconTextSizeProperty, value);
    }

    public double CornerRadius
    {
        get => (double)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public double IconTranslationX
    {
        get => (double)GetValue(IconTranslationXProperty);
        set => SetValue(IconTranslationXProperty, value);
    }

    public bool HasIconSource => !string.IsNullOrWhiteSpace(IconSource);

    public bool HasIconText => !string.IsNullOrWhiteSpace(IconText);

    private static void OnIconTextChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not ChromeGlassButton button)
            return;

        button.OnPropertyChanged(nameof(HasIconText));
    }

    private static void OnIconSourceChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not ChromeGlassButton button)
            return;

        button.OnPropertyChanged(nameof(HasIconSource));
    }

    private static void OnButtonSizeChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not ChromeGlassButton button || newValue is not double size)
            return;

        button.CornerRadius = size / 2d;
    }

    // Native-style Liquid Glass press (researched against iOS 26's
    // .glassEffect(.interactive())): button + glyph swell together significantly
    // while the glass brightens (surface tint animation), then spring back with a bounce on
    // release. No ripple — that's Android Material. Scale and glow run
    // concurrently (fire-and-forget) so neither delays the other.
    private void OnEffectsTouchDown(object? sender, EventArgs e)
    {
        _ = VisualFrame.ScaleToAsync(1.25, 140, Easing.CubicOut);
        // Brighten happens BELOW the glyph (the glass surface's own tint) — a
        // white overlay above the glyph washed the icon out, per device feedback.
        AnimateSurfaceTint(GlassPressedTint, 110, Easing.CubicOut);
    }

    private void OnEffectsTouchUp(object? sender, EventArgs e)
    {
        // The visual release ALWAYS runs. Previously the click guard sat above it,
        // so a TouchUp arriving during the prior press's settle window was
        // swallowed whole — leaving the button stuck at 1.25x with the pressed
        // tint (reproduced with press-and-hold / quick re-presses).
        if (_pressSurfaceRestTint is { } rest)
            AnimateSurfaceTint(rest, 300, Easing.CubicInOut);
        _ = VisualFrame.ScaleToAsync(1.0, 340, Easing.SpringOut);

        // Only the click DISPATCH is de-duped, on a short time window — time-based
        // so the guard can never wedge shut. Clicked fires immediately (native
        // glass taps respond instantly; animations run concurrently).
        if (_isClickDispatching)
            return;

        _isClickDispatching = true;
        Clicked?.Invoke(this, EventArgs.Empty);
        _ = ResetClickGuardAsync();
    }

    private async Task ResetClickGuardAsync()
    {
        await Task.Delay(250);
        _isClickDispatching = false;
    }

    private void AnimateSurfaceTint(Color to, uint length, Easing easing)
    {
        if (_pressSurface is null)
            return;

        _pressSurfaceRestTint ??= _pressSurface.BackgroundColor;
        var from = _pressSurface.BackgroundColor ?? Colors.Transparent;
        this.AbortAnimation("PressTint");
        new Animation(t => _pressSurface.BackgroundColor = LerpColor(from, to, (float)t))
            .Commit(this, "PressTint", 16, length, easing);
    }

    private static Color LerpColor(Color a, Color b, float t) => new(
        a.Red + (b.Red - a.Red) * t,
        a.Green + (b.Green - a.Green) * t,
        a.Blue + (b.Blue - a.Blue) * t,
        a.Alpha + (b.Alpha - a.Alpha) * t);

}
