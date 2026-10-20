using CommunityToolkit.Mvvm.Input;

namespace Rpaso.ViewModels;

/// <summary>
/// Vista de una carpeta: sus tests y las acciones de práctica.
/// </summary>
public sealed class FolderViewModel : ViewModelBase
{
    private readonly MainViewModel _principal;

    public FolderViewModel(MainViewModel principal, CarpetaViewModel carpeta)
    {
        _principal = principal;
        Carpeta = carpeta;
    }

    public CarpetaViewModel Carpeta { get; }

    public string Nombre => Carpeta.Nombre;

    public string Descripcion => Carpeta.Descripcion;

    public IReadOnlyList<TestViewModel> Tests => Carpeta.Tests;

    public int TotalFalladas => Carpeta.TotalFalladas;

    public int TotalPreguntas => Carpeta.TotalPreguntas;

    public bool HayFalladas => TotalFalladas > 0;

    public bool HayTests => Tests.Count > 0;

    public IRelayCommand VolverCommand => _principal.IrAInicioCommand;

    public IRelayCommand PracticarTestCommand => _principal.PracticarTestCommand;

    public IRelayCommand PracticarTestFalladasCommand => _principal.PracticarTestFalladasCommand;

    public IRelayCommand PracticarCarpetaCommand => _principal.PracticarCarpetaCommand;

    public IRelayCommand PracticarCarpetaFalladasCommand => _principal.PracticarCarpetaFalladasCommand;

    public IRelayCommand ImportarPdfCommand => _principal.ImportarPdfCommand;
}
