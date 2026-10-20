using CommunityToolkit.Mvvm.Input;

namespace Rpaso.ViewModels;

/// <summary>
/// Vista de inicio: métricas globales y rejilla de carpetas.
/// </summary>
public sealed class HomeViewModel : ViewModelBase
{
    private readonly MainViewModel _principal;

    public HomeViewModel(MainViewModel principal)
    {
        _principal = principal;

        // HomeViewModel expone datos del padre. Sin reenviar sus PropertyChanged,
        // los bindings de esta vista se quedarían con el valor inicial.
        _principal.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(MainViewModel.TotalTests):
                    OnPropertyChanged(nameof(TotalTests));
                    break;
                case nameof(MainViewModel.TotalPreguntas):
                    OnPropertyChanged(nameof(TotalPreguntas));
                    break;
                case nameof(MainViewModel.TotalFalladas):
                    OnPropertyChanged(nameof(TotalFalladas));
                    OnPropertyChanged(nameof(HayFalladas));
                    break;
                case nameof(MainViewModel.Carpetas):
                    OnPropertyChanged(nameof(Carpetas));
                    OnPropertyChanged(nameof(TextoCarpetas));
                    break;
            }
        };
    }

    public IReadOnlyList<CarpetaViewModel> Carpetas => _principal.Carpetas;

    public int TotalTests => _principal.TotalTests;

    public int TotalPreguntas => _principal.TotalPreguntas;

    public int TotalFalladas => _principal.TotalFalladas;

    public bool HayFalladas => _principal.TotalFalladas > 0;

    public string TextoCarpetas => Carpetas.Count == 1 ? "1 carpeta activa" : $"{Carpetas.Count} carpetas activas";

    public IRelayCommand AbrirCarpetaCommand => _principal.AbrirCarpetaCommand;

    public IRelayCommand ImportarPdfCommand => _principal.ImportarPdfCommand;

    public IRelayCommand AbrirModalCarpetaCommand => _principal.AbrirModalCarpetaCommand;

    public IRelayCommand RenombrarCarpetaCommand => _principal.RenombrarCarpetaCommand;

    public IRelayCommand EliminarCarpetaCommand => _principal.EliminarCarpetaCommand;

    public IRelayCommand PracticarCarpetaCommand => _principal.PracticarCarpetaCommand;

    public IRelayCommand PracticarCarpetaFalladasCommand => _principal.PracticarCarpetaFalladasCommand;
}
