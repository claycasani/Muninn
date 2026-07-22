using Foundation;

namespace Muninn;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

	public override bool FinishedLaunching(UIKit.UIApplication application, NSDictionary? launchOptions)
	{
		var result = base.FinishedLaunching(application, launchOptions);

		// Set a global nav-bar appearance proxy.
		//
		// ScrollEdgeAppearance (large-title / scroll-at-top state): transparent so the
		// page's own ColorBackground (#FAFAF8) cream shows through seamlessly — no
		// white-vs-cream seam. This also works on iOS 26 Liquid Glass where an opaque
		// background colour may be overridden by the system material.
		//
		// StandardAppearance (scrolled-down / compact title state): opaque cream so
		// the nav bar remains readable when content scrolls under it.
		try
		{
			var bgColor  = UIKit.UIColor.FromRGB(0xFA, 0xFA, 0xF8);
			var sepColor = UIKit.UIColor.FromRGB(0xE5, 0xE5, 0xEA);

			// Transparent — used at scroll-top (large title visible)
			var transparentAppearance = new UIKit.UINavigationBarAppearance();
			transparentAppearance.ConfigureWithTransparentBackground();
			transparentAppearance.ShadowColor = UIKit.UIColor.Clear;

			// Opaque cream — used when scrolled down (compact title)
			var opaqueAppearance = new UIKit.UINavigationBarAppearance();
			opaqueAppearance.ConfigureWithOpaqueBackground();
			opaqueAppearance.BackgroundColor = bgColor;
			opaqueAppearance.ShadowColor     = sepColor;

			UIKit.UINavigationBar.Appearance.ScrollEdgeAppearance        = transparentAppearance;
			UIKit.UINavigationBar.Appearance.CompactScrollEdgeAppearance = transparentAppearance;
			UIKit.UINavigationBar.Appearance.StandardAppearance          = opaqueAppearance;
			UIKit.UINavigationBar.Appearance.CompactAppearance           = opaqueAppearance;
		}
		catch { /* cosmetic — never crash the app */ }

		return result;
	}
}
