using Foundation;
using ObjCRuntime;
using UIKit;

namespace MuninnShare;

/// <summary>
/// iOS Share Extension entry point.
///
/// This extension extracts the shared URL immediately in ViewDidLoad, writes it to the
/// App Group NSUserDefaults suite, briefly shows a native confirmation, and calls
/// CompleteRequest.
///
/// The main Muninn app reads the pending URL via Preferences.Get(key, "", groupId) after
/// a valid session is confirmed, creates a save via the backend, and clears the key only
/// on success.  A failed network call leaves the key intact so the next session retries.
///
/// App Group ID: group.com.claycasani.muninn
/// Key:          pendingShareUrl
/// </summary>
[Register("ShareViewController")]
public class ShareViewController : UIViewController
{
    private const string AppGroupId = "group.com.claycasani.muninn";
    private const string PendingShareKey = "pendingShareUrl";
    private int _didComplete;
    private UILabel? _statusLabel;

    // Required constructor — the OS instantiates the extension via this handle.
    protected ShareViewController(NativeHandle handle) : base(handle) { }

    public override void ViewDidLoad()
    {
        try
        {
            ViewDidLoadImpl();
        }
        catch (Exception ex)
        {
            // Write to the extension's own temp dir (sandbox-accessible); host /tmp is not writable.
            try
            {
                var path = Path.Combine(Path.GetTempPath(), "muninn_share_crash.txt");
                File.WriteAllText(path, ex.ToString());
            }
            catch { }
            SafeDone();
        }
    }

    private void ViewDidLoadImpl()
    {
        base.ViewDidLoad();

        BuildConfirmationView();

        // InputItems can be null at the ObjC layer even when ExtensionContext is non-null.
        var firstItem = ExtensionContext?.InputItems?
            .OfType<NSExtensionItem>()
            .FirstOrDefault();

        var urlProvider = firstItem?.Attachments?
            .FirstOrDefault(p => p.HasItemConformingTo("public.url"));

        if (urlProvider is null)
        {
            ShowStatusAndComplete("Nothing to save.");
            return;
        }

        urlProvider.LoadItem("public.url", options: null, completionHandler: (item, error) =>
        {
            try
            {
                if (error is null)
                {
                    string? urlString = item switch
                    {
                        NSUrl nsUrl    => nsUrl.AbsoluteString,
                        NSString nsStr => nsStr.ToString(),
                        _              => null
                    };

                    if (!string.IsNullOrEmpty(urlString))
                    {
                        var defaults = new NSUserDefaults(AppGroupId, NSUserDefaultsType.SuiteName);
                        defaults.SetString(urlString, PendingShareKey);
                        defaults.Synchronize();
                        ShowStatusAndComplete("Webpage saved.");
                        return;
                    }
                }

                ShowStatusAndComplete("Nothing to save.");
            }
            catch (Exception ex)
            {
                try
                {
                    var path = Path.Combine(Path.GetTempPath(), "muninn_share_crash.txt");
                    File.AppendAllText(path, "\n[callback] " + ex);
                }
                catch { }
                ShowStatusAndComplete("Couldn't save webpage.");
            }
        });
    }

    private void BuildConfirmationView()
    {
        if (View is null) return;

        // Mirrors the app styleguide in native UIKit: cream background, white card,
        // muted olive success mark, calm typography.
        View.BackgroundColor = UIColor.FromRGB(0xFA, 0xFA, 0xF8);

        var card = new UIView
        {
            TranslatesAutoresizingMaskIntoConstraints = false,
            BackgroundColor = UIColor.White
        };
        card.Layer.CornerRadius = 16;
        card.Layer.ShadowColor = UIColor.Black.CGColor;
        card.Layer.ShadowOpacity = 0.06f;
        card.Layer.ShadowRadius = 8;
        card.Layer.ShadowOffset = new CoreGraphics.CGSize(0, 2);

        var imageView = new UIImageView
        {
            TranslatesAutoresizingMaskIntoConstraints = false,
            Image = UIImage.GetSystemImage("checkmark.circle.fill"),
            TintColor = UIColor.FromRGB(0x44, 0x4A, 0x2E),
            ContentMode = UIViewContentMode.ScaleAspectFit
        };

        var statusLabel = new UILabel
        {
            TranslatesAutoresizingMaskIntoConstraints = false,
            Text = "Saving webpage...",
            TextColor = UIColor.FromRGB(0x1A, 0x1A, 0x1A),
            Font = UIFont.SystemFontOfSize(17, UIFontWeight.Semibold)!,
            Lines = 1,
            TextAlignment = UITextAlignment.Center
        };
        _statusLabel = statusLabel;

        var subtitleLabel = new UILabel
        {
            TranslatesAutoresizingMaskIntoConstraints = false,
            Text = "Return to Muninn to see it in Inbox.",
            TextColor = UIColor.FromRGB(0x8E, 0x8E, 0x93),
            Font = UIFont.SystemFontOfSize(13)!,
            Lines = 2,
            TextAlignment = UITextAlignment.Center
        };

        View.AddSubview(card);
        card.AddSubview(imageView);
        card.AddSubview(statusLabel);
        card.AddSubview(subtitleLabel);

        NSLayoutConstraint.ActivateConstraints(new[]
        {
            card.CenterXAnchor.ConstraintEqualTo(View.CenterXAnchor),
            card.CenterYAnchor.ConstraintEqualTo(View.CenterYAnchor, -40),
            card.LeadingAnchor.ConstraintGreaterThanOrEqualTo(View.LeadingAnchor, 24),
            card.TrailingAnchor.ConstraintLessThanOrEqualTo(View.TrailingAnchor, -24),
            card.WidthAnchor.ConstraintLessThanOrEqualTo(320),

            imageView.TopAnchor.ConstraintEqualTo(card.TopAnchor, 24),
            imageView.CenterXAnchor.ConstraintEqualTo(card.CenterXAnchor),
            imageView.WidthAnchor.ConstraintEqualTo(36),
            imageView.HeightAnchor.ConstraintEqualTo(36),

            statusLabel.TopAnchor.ConstraintEqualTo(imageView.BottomAnchor, 12),
            statusLabel.LeadingAnchor.ConstraintEqualTo(card.LeadingAnchor, 20),
            statusLabel.TrailingAnchor.ConstraintEqualTo(card.TrailingAnchor, -20),

            subtitleLabel.TopAnchor.ConstraintEqualTo(statusLabel.BottomAnchor, 6),
            subtitleLabel.LeadingAnchor.ConstraintEqualTo(card.LeadingAnchor, 20),
            subtitleLabel.TrailingAnchor.ConstraintEqualTo(card.TrailingAnchor, -20),
            subtitleLabel.BottomAnchor.ConstraintEqualTo(card.BottomAnchor, -24)
        });
    }

    private void ShowStatusAndComplete(string message)
    {
        void UpdateAndComplete()
        {
            if (_statusLabel is not null)
                _statusLabel.Text = message;

            Task.Delay(650).ContinueWith(_ => SafeDone());
        }

        if (NSThread.IsMain)
            UpdateAndComplete();
        else
            InvokeOnMainThread(UpdateAndComplete);
    }

    // Pass an empty array (not null) — the binding's NSExtensionItem[] param is non-nullable,
    // and passing null! causes an ArgumentNullException in the marshaling layer.
    private void SafeDone()
    {
        if (Interlocked.Exchange(ref _didComplete, 1) != 0) return;

        void Complete()
        {
            try { ExtensionContext?.CompleteRequest(returningItems: Array.Empty<NSExtensionItem>(), completionHandler: null); }
            catch { }
        }

        if (NSThread.IsMain)
            Complete();
        else
            InvokeOnMainThread(Complete);
    }
}
