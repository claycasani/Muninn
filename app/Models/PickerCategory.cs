using CommunityToolkit.Mvvm.ComponentModel;

namespace Muninn.Models;

/// <summary>
/// A category chip in the Save Detail picker. Holds its own selection state so a
/// tapped chip highlights (green) immediately without a server round-trip; the
/// choice is only applied when the user confirms.
/// </summary>
public partial class PickerCategory : ObservableObject
{
    private static readonly Color Accent = Color.FromArgb("#444A2E");
    private static readonly Color Sunken = Color.FromArgb("#F2F2EF");
    private static readonly Color TextPrimary = Color.FromArgb("#1A1A1A");

    public string Name { get; }

    public PickerCategory(string name, bool isSelected)
    {
        Name = name;
        _isSelected = isSelected;
    }

    [ObservableProperty]
    private bool _isSelected;

    public Color ChipBackground => IsSelected ? Accent : Sunken;
    public Color ChipTextColor => IsSelected ? Colors.White : TextPrimary;

    partial void OnIsSelectedChanged(bool value)
    {
        OnPropertyChanged(nameof(ChipBackground));
        OnPropertyChanged(nameof(ChipTextColor));
    }
}
