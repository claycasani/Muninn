using System.Collections.ObjectModel;

namespace Muninn.Models;

/// <summary>
/// A named group of saves for the grouped CollectionView in InboxPage.
/// Extends ObservableCollection so additions/removals propagate to the UI.
/// </summary>
public class SaveGroup : ObservableCollection<SaveModel>
{
    public string Category { get; }

    public SaveGroup(string category, IEnumerable<SaveModel> saves) : base(saves)
    {
        Category = category;
    }
}
