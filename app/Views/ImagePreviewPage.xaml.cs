namespace Muninn.Views;

public partial class ImagePreviewPage : ContentPage
{
    public ImagePreviewPage(ImageSource imageSource)
    {
        InitializeComponent();
        PreviewImage.Source = imageSource;
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
#if IOS
        if (Handler?.PlatformView is not UIKit.UIView nativeView) return;

        // Make the MAUI page background transparent so the blur layer is the visual background.
        nativeView.BackgroundColor = UIKit.UIColor.Clear;

        // Walk the responder chain to find the ViewController for this modal so we can
        // set OverFullScreen — this keeps the presenting VC in the view hierarchy, which
        // gives the blur effect actual content to blur rather than rendering against black.
        UIKit.UIResponder? responder = nativeView.NextResponder;
        while (responder is not null)
        {
            if (responder is UIKit.UIViewController vc)
            {
                vc.ModalPresentationStyle = UIKit.UIModalPresentationStyle.OverFullScreen;
                break;
            }
            responder = responder.NextResponder;
        }

        // Dark blur background matching iOS 26 Liquid Glass modal sheets.
        var blurEffect = UIKit.UIBlurEffect.FromStyle(UIKit.UIBlurEffectStyle.SystemUltraThinMaterialDark);
        var blurView = new UIKit.UIVisualEffectView(blurEffect)
        {
            Frame = nativeView.Bounds,
            AutoresizingMask = UIKit.UIViewAutoresizing.FlexibleWidth | UIKit.UIViewAutoresizing.FlexibleHeight
        };
        nativeView.InsertSubview(blurView, 0);
#endif
    }

    private async void OnCloseTapped(object? sender, TappedEventArgs e)
    {
        await Navigation.PopModalAsync();
    }
}
