using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Rpaso.ViewModels;

/// <summary>
/// Vista de una carpeta: sus tests y las acciones de práctica.
/// </summary>
public sealed partial class FolderViewModel : ViewModelBase
{
    private readonly MainViewModel _principal;

    [ObservableProperty]
    private bool _modoSeleccion;

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

    public IRelayCommand ImportarPdfCommand => _principal.ImportarPdfCommand;

    /// <summary>Abre el editor de preguntas del examen.</summary>
    [RelayCommand]
    private void EditarTest(TestViewModel? test)
    {
        if (test is null)
            return;

        _principal.AbrirEditorCommand.Execute(test);
    }

    /// <summary>Renombra el examen.</summary>
    [RelayCommand]
    private void RenombrarTest(TestViewModel? test)
    {
        if (test is null)
            return;

        _principal.RenombrarTestCommand.Execute(test);
    }

    /// <summary>Elimina el examen.</summary>
    [RelayCommand]
    private void EliminarTest(TestViewModel? test)
    {
        if (test is null)
            return;

        _principal.EliminarTestCommand.Execute(test);
    }

    [RelayCommand]
    private void PracticarSeleccion()
    {
        if (!ModoSeleccion || !HaySeleccion)
            return;

        var seleccionados = Seleccionados;
        CancelarSeleccion();
        _principal.PracticarSeleccion(seleccionados);
    }

    [RelayCommand]
    private void IniciarSeleccion()
    {
        foreach (var test in Tests)
            test.Seleccionada = false;

        ModoSeleccion = true;
    }

    [RelayCommand]
    private void CancelarSeleccion()
    {
        ModoSeleccion = false;
        foreach (var test in Tests)
            test.Seleccionada = false;
    }

    [RelayCommand]
    private void SeleccionarTodo()
    {
        var marcar = !Tests.All(t => t.Seleccionada);

        foreach (var test in Tests)
            test.Seleccionada = marcar;
    }
}
