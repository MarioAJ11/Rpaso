using CommunityToolkit.Mvvm.Input;

namespace Rpaso.ViewModels;

/// <summary>
/// Vista de una carpeta: sus tests y las acciones de práctica.
/// </summary>
public sealed partial class FolderViewModel : ViewModelBase
{
    private readonly MainViewModel _principal;

    public FolderViewModel(MainViewModel principal, CarpetaViewModel carpeta)
    {
        _principal = principal;
        Carpeta = carpeta;

        SuscribirTests();
    }

    public CarpetaViewModel Carpeta { get; private set; }

    /// <summary>
    /// La selección vive en cada test: hay que reaccionar a sus cambios para
    /// actualizar el contador y el botón de práctica combinada.
    /// </summary>
    private void SuscribirTests()
    {
        foreach (var test in Tests)
        {
            test.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != nameof(TestViewModel.Seleccionada))
                    return;

                OnPropertyChanged(nameof(Seleccionados));
                OnPropertyChanged(nameof(HaySeleccion));
                OnPropertyChanged(nameof(TextoSeleccion));
            };
        }
    }

    /// <summary>Ids de los tests marcados para la práctica combinada.</summary>
    public IReadOnlyList<string> IdsSeleccionados =>
        Tests.Where(t => t.Seleccionada).Select(t => t.Id).ToList();

    /// <summary>
    /// Refresca la vista cuando cambian los datos (renombrar, eliminar, importar).
    /// Conserva los tests marcados para la práctica combinada.
    /// </summary>
    public void Refrescar(IReadOnlyList<string>? idsSeleccionados = null)
    {
        var conservar = idsSeleccionados ?? IdsSeleccionados;

        SuscribirTests();

        foreach (var test in Tests)
            test.Seleccionada = conservar.Contains(test.Id);

        OnPropertyChanged(nameof(Carpeta));
        OnPropertyChanged(nameof(Nombre));
        OnPropertyChanged(nameof(Descripcion));
        OnPropertyChanged(nameof(Tests));
        OnPropertyChanged(nameof(TotalFalladas));
        OnPropertyChanged(nameof(TotalPreguntas));
        OnPropertyChanged(nameof(HayFalladas));
        OnPropertyChanged(nameof(HayTests));
        OnPropertyChanged(nameof(Seleccionados));
        OnPropertyChanged(nameof(HaySeleccion));
        OnPropertyChanged(nameof(TextoSeleccion));
    }

    public string Nombre => Carpeta.Nombre;

    public string Descripcion => Carpeta.Descripcion;

    public IReadOnlyList<TestViewModel> Tests => Carpeta.Tests;

    public int TotalFalladas => Carpeta.TotalFalladas;

    public int TotalPreguntas => Carpeta.TotalPreguntas;

    public bool HayFalladas => TotalFalladas > 0;

    public bool HayTests => Tests.Count > 0;

    /// <summary>Tests marcados para la práctica combinada.</summary>
    public IReadOnlyList<TestViewModel> Seleccionados => Tests.Where(t => t.Seleccionada).ToList();

    public bool HaySeleccion => Seleccionados.Count > 0;

    public string TextoSeleccion => $"{Seleccionados.Count} seleccionados";

    public IRelayCommand VolverCommand => _principal.IrAInicioCommand;

    public IRelayCommand PracticarTestCommand => _principal.PracticarTestCommand;

    public IRelayCommand PracticarTestFalladasCommand => _principal.PracticarTestFalladasCommand;

    public IRelayCommand PracticarCarpetaCommand => _principal.PracticarCarpetaCommand;

    public IRelayCommand PracticarCarpetaFalladasCommand => _principal.PracticarCarpetaFalladasCommand;

    public IRelayCommand AbrirEditorCommand => _principal.AbrirEditorCommand;

    public IRelayCommand RenombrarTestCommand => _principal.RenombrarTestCommand;

    public IRelayCommand EliminarTestCommand => _principal.EliminarTestCommand;

    public IRelayCommand ImportarPdfCommand => _principal.ImportarPdfCommand;

    [RelayCommand]
    private void PracticarSeleccion()
    {
        if (!HaySeleccion)
            return;

        _principal.PracticarSeleccion(Seleccionados);
    }

    [RelayCommand]
    private void SeleccionarTodo()
    {
        var marcar = !Tests.All(t => t.Seleccionada);

        foreach (var test in Tests)
            test.Seleccionada = marcar;
    }
}
