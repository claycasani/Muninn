using Microsoft.Maui.Graphics;

namespace Muninn.Models;

public class CategoryFilterOption
{
    public CategoryFilterOption(string name, bool isSelected, Color backgroundColor, Color textColor)
    {
        Name = name;
        IsSelected = isSelected;
        BackgroundColor = backgroundColor;
        TextColor = textColor;
    }

    public string Name { get; }
    public bool IsSelected { get; }
    public Color BackgroundColor { get; }
    public Color TextColor { get; }
}
