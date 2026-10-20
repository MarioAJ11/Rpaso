using CommunityToolkit.Mvvm.ComponentModel;

namespace Rpaso.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _greeting = "Cambiado desde el ViewModel";
}
