namespace Muninn.Controls;

public partial class SearchBarView : ContentView
{
    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text),
        typeof(string),
        typeof(SearchBarView),
        string.Empty,
        BindingMode.TwoWay);

    public static readonly BindableProperty PlaceholderProperty = BindableProperty.Create(
        nameof(Placeholder),
        typeof(string),
        typeof(SearchBarView),
        "Search saves…");

    // Fired when the user COMMITS the search (Done key or tapping away). Filtering
    // happens only then: any list rebuild while the Entry is focused — even a
    // granular batch update — makes the CollectionView resign the keyboard (proven
    // on-device via SSDIAG ordering), so live-as-you-type filtering is impossible
    // with the search bar living in the CV header.
    public static readonly BindableProperty CommitCommandProperty = BindableProperty.Create(
        nameof(CommitCommand),
        typeof(System.Windows.Input.ICommand),
        typeof(SearchBarView));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public System.Windows.Input.ICommand? CommitCommand
    {
        get => (System.Windows.Input.ICommand?)GetValue(CommitCommandProperty);
        set => SetValue(CommitCommandProperty, value);
    }

    public SearchBarView()
    {
        InitializeComponent();
    }

    private void OnSearchUnfocused(object? sender, FocusEventArgs e)
    {
        SearchBorder.StrokeThickness = 1;
        SearchBorder.Stroke = ResourceColor("ColorSeparator");
        // Keyboard is going away regardless — safe to rebuild the list now.
        CommitCommand?.Execute(null);
    }

    private void OnSearchCompleted(object? sender, EventArgs e)
    {
        SearchEntry.Unfocus();
    }

    private void OnClearClicked(object? sender, EventArgs e)
    {
        Text = string.Empty;
        SearchEntry.Focus();
    }

    private static Color ResourceColor(string key)
    {
        return Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color
            ? color
            : Colors.Transparent;
    }
}
