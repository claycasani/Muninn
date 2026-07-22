using Muninn.ViewModels;
using System.ComponentModel;
using Muninn.Diagnostics;

namespace Muninn.Views;

public partial class InboxPage : ContentPage
{
    private const uint LinkBarAnimationDuration = 300;

    // Minimum gap between loads triggered by tab navigation. Explicit refreshes
    // (e.g. App.ShowInboxAfterPendingShareAsync) are not subject to this guard.
    private static readonly TimeSpan MinReloadInterval = TimeSpan.FromSeconds(10);

    private readonly InboxViewModel _viewModel;
    private DateTime _lastLoadAt = DateTime.MinValue;

    public InboxPage(InboxViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        ApplyLinkBarState(_viewModel.IsLinkBarVisible);

        if (LaunchDiagnostics.HideInboxChrome)
        {
            TopChromeBar.IsVisible = false;
            BottomChromeBar.IsVisible = false;
            Console.WriteLine("[SSDIAG] Inbox chrome hidden by launch diagnostics");
        }

        if (LaunchDiagnostics.InboxStdLayout)
        {
            // Conventional layout: respect safe areas and drop the negative bottom
            // margin that pulls the CollectionView frame into the bottom inset.
            SafeAreaEdges = SafeAreaEdges.Default;
            SavesCollection.Margin = new Thickness(24, 0, 24, 0);
            Console.WriteLine("[SSDIAG] Inbox using standard layout (safe areas on, no negative margin) by launch diagnostics");
        }

        // Removal (not hiding): pulls the subtree out of the page before it is
        // hosted, so no native views/handlers are ever created for it. This is the
        // lever HideInboxChrome (IsVisible=false) can't provide for a
        // compositor-level failure.
        if (LaunchDiagnostics.InboxNoCollection)
        {
            RootGrid.Remove(SavesCollection);
            Console.WriteLine("[SSDIAG] Inbox diagnostic: CollectionView (incl. header content) REMOVED from tree");
        }

        if (LaunchDiagnostics.InboxNoChromeTree)
        {
            RootGrid.Remove(TopChromeBar);
            RootGrid.Remove(BottomChromeBar);
            Console.WriteLine("[SSDIAG] Inbox diagnostic: chrome bars (glass buttons + tab bar) REMOVED from tree");
        }

        // Header split: the CV header is constructed even when the list is invisible
        // and empty — matching every crash. Null/remove pieces before hosting.
        if (LaunchDiagnostics.InboxNoCvHeader)
        {
            SavesCollection.Header = null;
            Console.WriteLine("[SSDIAG] Inbox diagnostic: CollectionView.Header REMOVED entirely");
        }
        else
        {
            if (LaunchDiagnostics.InboxNoChips)
            {
                HeaderStack.Remove(CategoryChipsScroll);
                Console.WriteLine("[SSDIAG] Inbox diagnostic: category-chips ScrollView REMOVED from header");
            }

            if (LaunchDiagnostics.InboxNoSearch)
            {
                HeaderStack.Remove(SearchBar);
                Console.WriteLine("[SSDIAG] Inbox diagnostic: SearchBarView REMOVED from header");
            }

            if (LaunchDiagnostics.InboxNoLinkBar)
            {
                HeaderStack.Remove(LinkBarHost);
                Console.WriteLine("[SSDIAG] Inbox diagnostic: link bar REMOVED from header");
            }
        }

        if (LaunchDiagnostics.InboxBareCollection)
        {
            // A bare, code-built CollectionView: default item template, plain string
            // items, no header/group/empty templates, conventional margins. Combined
            // with InboxNoCollection this swaps Muninn's CollectionView for the
            // simplest possible one — if THIS resprings the device, the control's
            // native handler is implicated, independent of Muninn's content.
            var bare = new CollectionView
            {
                ItemsSource = Enumerable.Range(1, 40).Select(i => $"Diagnostic row {i}").ToList(),
                Margin = new Thickness(24, 80, 24, 0)
            };
            RootGrid.Add(bare);
            Console.WriteLine("[SSDIAG] Inbox diagnostic: BARE CollectionView (40 strings, default template) ADDED");
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (LaunchDiagnostics.DisableInboxRefresh)
        {
            Console.WriteLine("[SSDIAG] InboxPage.OnAppearing refresh skipped by launch diagnostics");
            return;
        }

        // Skip the reload if data was refreshed very recently (e.g. user is just
        // switching tabs back while a slow cloud request is already pending or just
        // completed). This prevents the tab-switch → OnAppearing → RefreshScreenshotCount
        // cycle from fetching the full photo library on every navigation.
        var now = DateTime.UtcNow;
        if (now - _lastLoadAt < MinReloadInterval)
            return;

        await RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        if (LaunchDiagnostics.DisableInboxRefresh)
        {
            Console.WriteLine("[SSDIAG] InboxPage.RefreshAsync skipped by launch diagnostics");
            return;
        }

        _lastLoadAt = DateTime.UtcNow;
        await _viewModel.LoadSavesCommand.ExecuteAsync(null);
        await _viewModel.RefreshScreenshotCountCommand.ExecuteAsync(null);
    }

    private async void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(InboxViewModel.IsLinkBarVisible))
            return;

        await AnimateLinkBarAsync(_viewModel.IsLinkBarVisible);
    }

    private void OnAddClicked(object? sender, EventArgs e)
    {
        _viewModel.ToggleLinkBarCommand.Execute(null);
    }

    private async void OnSettingsClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(SettingsPage));
    }

    private async void OnScreenshotsClicked(object? sender, EventArgs e)
    {
        await _viewModel.OpenScreenshotReviewCommand.ExecuteAsync(null);
    }

    private async Task AnimateLinkBarAsync(bool isVisible)
    {
        LinkEntry.Unfocus();

        if (isVisible)
        {
            LinkBarHost.InputTransparent = false;
            var expandedHeight = GetExpandedLinkBarHeight();
            // The content starts translated up under the clip and slides down into
            // place. TranslationY is a GPU transform that animates smoothly every
            // frame regardless of how coarsely the CollectionView header applies
            // the container height — so the input box is VISIBLY sliding even if
            // layout steps are chunky.
            LinkBarContent.TranslationY = -expandedHeight;
            await Task.WhenAll(
                AnimateLinkBarHeightAsync(LinkBarHost.HeightRequest, expandedHeight),
                LinkBarContent.TranslateToAsync(0, 0, LinkBarAnimationDuration, Easing.CubicInOut),
                LinkBarHost.FadeToAsync(1, LinkBarAnimationDuration, Easing.CubicInOut),
                AddButtonActiveHalo.FadeToAsync(1, LinkBarAnimationDuration, Easing.CubicInOut));
            return;
        }

        LinkBarHost.InputTransparent = true;
        var collapseFrom = LinkBarHost.HeightRequest;
        await Task.WhenAll(
            AnimateLinkBarHeightAsync(collapseFrom, 0),
            LinkBarContent.TranslateToAsync(0, -collapseFrom, LinkBarAnimationDuration, Easing.CubicInOut),
            LinkBarHost.FadeToAsync(0, LinkBarAnimationDuration, Easing.CubicInOut),
            AddButtonActiveHalo.FadeToAsync(0, LinkBarAnimationDuration, Easing.CubicInOut));
    }

    private void ApplyLinkBarState(bool isVisible)
    {
        var expandedHeight = isVisible ? GetExpandedLinkBarHeight() : 0;
        LinkBarHost.HeightRequest = expandedHeight;
        LinkBarHost.Opacity = isVisible ? 1 : 0;
        LinkBarHost.InputTransparent = !isVisible;
        AddButtonActiveHalo.Opacity = isVisible ? 1 : 0;
    }

    private double GetExpandedLinkBarHeight()
    {
        var width = LinkBarContent.Width > 0 ? LinkBarContent.Width : Width;
        var measured = LinkBarContent.Measure(width, double.PositiveInfinity).Height;
        return measured > 0 ? measured : 85;
    }

    private Task AnimateLinkBarHeightAsync(double start, double end)
    {
        var tcs = new TaskCompletionSource();
        this.AbortAnimation(nameof(LinkBarHost));

        var animation = new Animation(
            callback: height =>
            {
                LinkBarHost.HeightRequest = height;
                // The link bar lives in the CollectionView header, and iOS's
                // UICollectionView does not re-measure header size per animation
                // frame — without an explicit per-tick layout invalidation the
                // height change is applied once at the end, so the reveal "snapped"
                // instead of sliding.
                InvalidateSavesCollectionLayout();
            },
            start: start,
            end: end,
            easing: Easing.CubicInOut);

        animation.Commit(
            owner: this,
            name: nameof(LinkBarHost),
            rate: 16,
            length: LinkBarAnimationDuration,
            finished: (_, _) =>
            {
                InvalidateSavesCollectionLayout();
                tcs.TrySetResult();
            });

        return tcs.Task;
    }

#if IOS
    private UIKit.UICollectionView? _platformCollectionView;

    private void InvalidateSavesCollectionLayout()
    {
        _platformCollectionView ??= SavesCollection.Handler?.PlatformView is UIKit.UIView root
            ? root as UIKit.UICollectionView ?? FindCollectionView(root)
            : null;

        if (_platformCollectionView is null)
            return;

        // PerformWithoutAnimation + LayoutIfNeeded: apply the new header height
        // SYNCHRONOUSLY this frame. Without it UIKit batches/animates the layout
        // invalidations and the height change still lands as one visible jump.
        UIKit.UIView.PerformWithoutAnimation(() =>
        {
            _platformCollectionView.CollectionViewLayout?.InvalidateLayout();
            _platformCollectionView.LayoutIfNeeded();
        });
    }

    private static UIKit.UICollectionView? FindCollectionView(UIKit.UIView view)
    {
        foreach (var sub in view.Subviews)
        {
            if (sub is UIKit.UICollectionView cv)
                return cv;
            if (FindCollectionView(sub) is { } nested)
                return nested;
        }

        return null;
    }
#else
    private void InvalidateSavesCollectionLayout()
    {
    }
#endif
}
