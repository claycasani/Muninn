using CommunityToolkit.Mvvm.ComponentModel;

namespace Muninn.ViewModels;

/// <summary>
/// Base class for all ViewModels. Inherits ObservableObject from
/// CommunityToolkit.Mvvm, which provides INotifyPropertyChanged,
/// SetProperty, and source-generator support via [ObservableProperty].
/// </summary>
public class BaseViewModel : ObservableObject
{
}
