using CommunityToolkit.Mvvm.ComponentModel;

namespace Muninn.Models;

/// <summary>A user-editable category (mirrors the backend CategoryResponse).</summary>
public partial class CategoryModel : ObservableObject
{
    public long Id { get; set; }

    [ObservableProperty]
    private string _name = string.Empty;
}
