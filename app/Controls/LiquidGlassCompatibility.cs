using Muninn.Diagnostics;

namespace Muninn.Controls;

internal static class LiquidGlassCompatibility
{
    /// <summary>
    /// Whether the chrome controls use genuine Syncfusion Liquid Glass surfaces
    /// (SfGlassEffectView) or the plain Border fallback.
    ///
    /// History: this gate originally disabled glass on iOS 27 because the beta's
    /// device-wide resprings were (reasonably) pinned on the glass compositor
    /// surfaces. The July 2026 investigation exonerated glass completely — the
    /// resprings were 2 GB bitmaps decoded from unsized SVG assets (see DEVLOG
    /// 2026-07-02, "ROOT CAUSE FOUND"). Glass is therefore enabled everywhere.
    ///
    /// The MUNINN_DISABLE_GLASS launch lever is kept as a kill-switch: if a future
    /// iOS beta misbehaves, one env var flips every chrome surface back to the
    /// plain fallback — no code changes, no bisection.
    /// </summary>
    public static bool ShouldUseSyncfusionGlass => !LaunchDiagnostics.DisableGlass;
}
