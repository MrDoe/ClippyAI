using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ReactiveUI;
namespace ClippyAI.ViewModels;

public partial class ViewModelBase : ObservableObject
{
    protected ViewModelBase()
    {
        ErrorMessages = [];
    }
    
    [ObservableProperty]
    private ObservableCollection<string>? _errorMessages;
}
