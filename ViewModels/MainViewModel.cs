using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rpaso.Models;
using Rpaso.Services;

namespace Rpaso.ViewModels;

/// <summary>
/// ViewModel raíz: navegación, carpetas, importación y métricas globales.
/// </summary>
public sealed partial class MainViewModel : ViewModelBase
{
    private readonly IServicioAlmacenamiento _almacenamiento;
    private readonly IServicioImportacion _importacion;
    private readonly Func<Task<IReadOnlyList<string>>> _seleccionarPdfs;

    public MainViewModel(
        IServicioAlmacenamiento almacenamiento,
        IServicioImportacion importacion,
        Func<Task<IReadOnlyList<string>>> seleccionarPdfs)
    {
        _almacenamiento = almacenamiento;
        _importacion = importacion;
        _seleccionarPdfs = seleccionarPdfs;

        Inicio = new HomeViewModel(this);
        VistaActual = Inicio;
    }

    public HomeViewModel Inicio { get; }

    public ObservableCollection<CarpetaViewModel> Carpetas { get; } = [];

    /// <summary>Vista activa. El ViewLocator la convierte en el control correspondiente.</summary>
    [ObservableProperty]
    private object? _vistaActual;

    [ObservableProperty]
    private bool _ocupado;

    [ObservableProperty]
    private string _estado = string.Empty;

    [ObservableProperty]
    private bool _modalVisible;

    [ObservableProperty]
    private string _nuevaCarpetaNombre = string.Empty;

    [ObservableProperty]
    private string _nuevaCarpetaDescripcion = string.Empty;

    [ObservableProperty]
    private string _nuevaCarpetaColorId = "violet";

    /// <summary>Informe de la última importación (null si no se ha importado nada aún).</summary>
    [ObservableProperty]
    private InformeImportacionViewModel? _informeImportacion;

    /// <summary>True mientras se muestra el informe de importación.</summary>
    [ObservableProperty]
    private bool _mostrarInformeImportacion;

    public IReadOnlyList<ColorCarpetaViewModel> Colores { get; } =
        PaletaColores.Todos.Select(c => new ColorCarpetaViewModel(c)).ToList();

    public int TotalTests => Carpetas.Sum(c => c.NumeroPdfs);

    public int TotalPreguntas => Carpetas.Sum(c => c.TotalPreguntas);

    public int TotalFalladas => Carpetas.Sum(c => c.TotalFalladas);

    /// <summary>Nombre de la carpeta abierta, si la hay.</summary>
    public string? MigaCarpeta => (VistaActual as FolderViewModel)?.Nombre;

    /// <summary>Título del examen en curso, si lo hay.</summary>
    public string? MigaExamen => (VistaActual as QuizViewModel)?.Titulo;

    /// <summary>Carpeta donde se guardarán los PDFs importados.</summary>
    public string CarpetaDestinoId => (VistaActual as FolderViewModel)?.Carpeta.Id ?? string.Empty;

    partial void OnVistaActualChanged(object? value)
    {
        OnPropertyChanged(nameof(MigaCarpeta));
        OnPropertyChanged(nameof(MigaExamen));
        OnPropertyChanged(nameof(EsVistaInicio));
    }

    /// <summary>True si la vista activa es el inicio (resalta el botón de la barra lateral).</summary>
    public bool EsVistaInicio => VistaActual is HomeViewModel;

    partial void OnNuevaCarpetaColorIdChanged(string value)
    {
        foreach (var color in Colores)
            color.Seleccionado = color.Id == value;
    }

    [RelayCommand]
    private void SeleccionarColor(string? colorId)
    {
        if (!string.IsNullOrWhiteSpace(colorId))
            NuevaCarpetaColorId = colorId;
    }

    /// <summary>Carga carpetas, tests y progreso desde el disco.</summary>
    public async Task CargarDatosAsync()
    {
        var carpetas = await _almacenamiento.CargarCarpetasAsync();
        var tests = await _almacenamiento.CargarTestsAsync();

        var progreso = new Dictionary<string, ProgresoPregunta>();
        foreach (var registro in await _almacenamiento.CargarProgresoAsync())
            progreso[registro.PreguntaId] = registro;

        Carpetas.Clear();
        foreach (var carpeta in carpetas)
            Carpetas.Add(new CarpetaViewModel(carpeta, tests, progreso));

        OnPropertyChanged(nameof(Carpetas));
        OnPropertyChanged(nameof(TotalTests));
        OnPropertyChanged(nameof(TotalPreguntas));
        OnPropertyChanged(nameof(TotalFalladas));
    }

    [RelayCommand]
    private void IrAInicio() => VistaActual = Inicio;

    [RelayCommand]
    private void AbrirCarpeta(CarpetaViewModel? carpeta)
    {
        if (carpeta is null)
            return;

        VistaActual = new FolderViewModel(this, carpeta);
    }

    [RelayCommand]
    private async Task ImportarPdfAsync()
    {
        var rutas = await _seleccionarPdfs();

        if (rutas.Count == 0)
            return;

        await ImportarVariosAsync(rutas);
    }

    /// <summary>
    /// Importa varios PDFs. Los que fallan no detienen el resto: al final se muestra
    /// un informe con lo importado y el motivo de cada fallo.
    /// </summary>
    public async Task ImportarVariosAsync(IReadOnlyList<string> rutas)
    {
        try
        {
            Ocupado = true;

            var progreso = new Progress<string>(archivo => Estado = $"Procesando {archivo}…");

            var informe = await _importacion.ImportarVariosAsync(rutas, CarpetaDestinoId, progreso);

            await CargarDatosAsync();

            InformeImportacion = new InformeImportacionViewModel(informe, () => MostrarInformeImportacion = false);
            MostrarInformeImportacion = true;

            Estado = informe.HayFallos
                ? $"{informe.TotalImportados} importados, {informe.TotalFallidos} con errores."
                : $"{informe.TotalImportados} exámenes importados.";
        }
        catch (Exception ex)
        {
            Estado = $"No se pudo importar: {ex.Message}";
        }
        finally
        {
            Ocupado = false;
        }
    }

    [RelayCommand]
    private void PracticarTest(TestViewModel? test)
    {
        if (test is null)
            return;

        VistaActual = CrearQuiz(test.Titulo, test.Preguntas);
    }

    [RelayCommand]
    private void PracticarTestFalladas(TestViewModel? test)
    {
        if (test is null)
            return;

        var falladas = test.PreguntasFalladas;

        if (falladas.Count == 0)
            return;

        VistaActual = CrearQuiz($"Falladas: {test.Titulo}", falladas);
    }

    [RelayCommand]
    private void PracticarCarpeta(CarpetaViewModel? carpeta)
    {
        if (carpeta is null)
            return;

        var preguntas = carpeta.Tests.SelectMany(t => t.Preguntas).ToList();

        if (preguntas.Count == 0)
            return;

        VistaActual = CrearQuiz($"Examen global: {carpeta.Nombre}", preguntas);
    }

    [RelayCommand]
    private void PracticarCarpetaFalladas(CarpetaViewModel? carpeta)
    {
        if (carpeta is null)
            return;

        var falladas = carpeta.Tests.SelectMany(t => t.PreguntasFalladas).ToList();

        if (falladas.Count == 0)
            return;

        VistaActual = CrearQuiz($"Repaso de falladas: {carpeta.Nombre}", falladas);
    }

    private QuizViewModel CrearQuiz(string titulo, IReadOnlyList<PreguntaViewModel> preguntas)
    {
        var vistaAnterior = VistaActual;
        return new QuizViewModel(titulo, preguntas, RegistrarResultado, () => VistaActual = vistaAnterior);
    }

    private void RegistrarResultado(string preguntaId, bool acertada)
    {
        var progreso = new ProgresoPregunta(
            preguntaId,
            acertada,
            acertada ? 0 : 1,
            DateTimeOffset.Now);

        _ = _almacenamiento.GuardarProgresoAsync(progreso);
    }

    [RelayCommand]
    private void AbrirModalCarpeta()
    {
        NuevaCarpetaNombre = string.Empty;
        NuevaCarpetaDescripcion = string.Empty;
        NuevaCarpetaColorId = "violet";
        ModalVisible = true;
    }

    [RelayCommand]
    private void CerrarModalCarpeta() => ModalVisible = false;

    [RelayCommand]
    private async Task CrearCarpetaAsync()
    {
        var nombre = NuevaCarpetaNombre.Trim();

        if (nombre.Length == 0)
            return;

        var carpeta = new Carpeta(
            Id: $"c-{Guid.NewGuid():N}"[..10],
            Nombre: nombre,
            Descripcion: NuevaCarpetaDescripcion.Trim(),
            ColorId: NuevaCarpetaColorId);

        await _almacenamiento.GuardarCarpetaAsync(carpeta);

        Carpetas.Add(new CarpetaViewModel(carpeta, [], new Dictionary<string, ProgresoPregunta>()));

        OnPropertyChanged(nameof(TotalTests));
        OnPropertyChanged(nameof(TotalPreguntas));
        OnPropertyChanged(nameof(TotalFalladas));

        ModalVisible = false;
    }
}
