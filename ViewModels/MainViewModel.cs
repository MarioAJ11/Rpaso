using System.Collections.ObjectModel;
using Avalonia.Threading;
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

    private DispatcherTimer? _temporizadorEstado;

    [ObservableProperty]
    private string _estado = string.Empty;

    /// <summary>
    /// El aviso flotante se borra solo a los pocos segundos.
    /// </summary>
    partial void OnEstadoChanged(string value)
    {
        _temporizadorEstado?.Stop();

        if (string.IsNullOrWhiteSpace(value))
            return;

        _temporizadorEstado = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _temporizadorEstado.Tick += (_, _) =>
        {
            _temporizadorEstado.Stop();
            Estado = string.Empty;
        };
        _temporizadorEstado.Start();
    }

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

    partial void OnModalColorIdChanged(string value)
    {
        foreach (var color in Colores)
            color.Seleccionado = color.Id == value;
    }

    [RelayCommand]
    private void SeleccionarColor(string? colorId)
    {
        if (!string.IsNullOrWhiteSpace(colorId))
            ModalColorId = colorId;
    }

    /// <summary>Carga carpetas, tests y progreso desde el disco.</summary>
    public async Task CargarDatosAsync()
    {
        var carpetas = await _almacenamiento.CargarCarpetasAsync();
        var tests = await _almacenamiento.CargarTestsAsync();

        var progreso = new Dictionary<string, ProgresoPregunta>();
        foreach (var registro in await _almacenamiento.CargarProgresoAsync())
            progreso[registro.PreguntaId] = registro;

        // Si hay una carpeta abierta, se recuerda qué tests estaban marcados.
        var idsSeleccionados = (VistaActual as FolderViewModel)?.IdsSeleccionados;

        // Se reaprovechan las instancias existentes: las vistas abiertas siguen
        // apuntando a la misma carpeta y así se refrescan al renombrar o eliminar.
        var existentes = Carpetas.ToDictionary(c => c.Id, StringComparer.OrdinalIgnoreCase);

        Carpetas.Clear();
        foreach (var carpeta in carpetas)
        {
            if (existentes.TryGetValue(carpeta.Id, out var vista))
            {
                vista.Actualizar(carpeta, tests, progreso);
                Carpetas.Add(vista);
            }
            else
            {
                Carpetas.Add(new CarpetaViewModel(carpeta, tests, progreso));
            }
        }

        OnPropertyChanged(nameof(Carpetas));
        OnPropertyChanged(nameof(TotalTests));
        OnPropertyChanged(nameof(TotalPreguntas));
        OnPropertyChanged(nameof(TotalFalladas));

        if (VistaActual is FolderViewModel carpetaAbierta)
            carpetaAbierta.Refrescar(idsSeleccionados);
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
    private void AbrirEditor(TestViewModel? test)
    {
        if (test is null)
            return;

        var carpeta = Carpetas.FirstOrDefault(c => c.Tests.Contains(test));

        VistaActual = new EditorTestViewModel(
            test.Test,
            GuardarTestEditado,
            () => VistaActual = carpeta is null ? Inicio : new FolderViewModel(this, carpeta));
    }

    private async Task GuardarTestEditado(Test test)
    {
        await _almacenamiento.GuardarTestAsync(test);
        await CargarDatosAsync();

        var carpeta = Carpetas.FirstOrDefault(c => c.Id == test.CarpetaId);

        VistaActual = carpeta is null ? Inicio : new FolderViewModel(this, carpeta);
        Estado = $"Guardado «{test.Titulo}».";
    }

    // ─────────────── Modal genérico (renombrar / confirmar) ───────────────

    [ObservableProperty] private bool _modalVisible;
    [ObservableProperty] private string _modalTitulo = string.Empty;
    [ObservableProperty] private string _modalDescripcion = string.Empty;
    [ObservableProperty] private string _modalValor = string.Empty;
    [ObservableProperty] private bool _modalMostrarEntrada;
    [ObservableProperty] private bool _modalEsPeligroso;
    [ObservableProperty] private bool _modalMostrarColores;
    [ObservableProperty] private string _modalTextoBoton = "Aceptar";
    [ObservableProperty] private string _modalColorId = "violet";
    [ObservableProperty] private string _modalDescripcionCarpeta = string.Empty;

    private Func<Task>? _modalAccion;

    [RelayCommand]
    private void CerrarModal()
    {
        ModalVisible = false;
        _modalAccion = null;
    }

    [RelayCommand]
    private async Task AceptarModalAsync()
    {
        var accion = _modalAccion;
        _modalAccion = null;
        ModalVisible = false;

        if (accion is not null)
            await accion();
    }

    private void AbrirModal(
        string titulo, string descripcion, string valor,
        bool mostrarEntrada, bool peligroso, Func<Task> accion,
        string textoBoton = "Aceptar", bool mostrarColores = false,
        string descripcionCarpeta = "", string colorId = "violet")
    {
        ModalTitulo = titulo;
        ModalDescripcion = descripcion;
        ModalValor = valor;
        ModalMostrarEntrada = mostrarEntrada;
        ModalEsPeligroso = peligroso;
        ModalTextoBoton = textoBoton;
        ModalMostrarColores = mostrarColores;
        ModalDescripcionCarpeta = descripcionCarpeta;
        ModalColorId = colorId;
        _modalAccion = accion;
        ModalVisible = true;
    }

    private async Task CrearCarpeta()
    {
        var nombre = ModalValor.Trim();

        if (nombre.Length == 0)
            return;

        var carpeta = new Carpeta(
            Id: $"c-{Guid.NewGuid():N}"[..10],
            Nombre: nombre,
            Descripcion: ModalDescripcionCarpeta.Trim(),
            ColorId: ModalColorId);

        await _almacenamiento.GuardarCarpetaAsync(carpeta);

        Carpetas.Add(new CarpetaViewModel(carpeta, [], new Dictionary<string, ProgresoPregunta>()));

        OnPropertyChanged(nameof(TotalTests));
        OnPropertyChanged(nameof(TotalPreguntas));
        OnPropertyChanged(nameof(TotalFalladas));

        Estado = $"Carpeta «{nombre}» creada.";
    }

    // ─────────────── Carpetas ───────────────

    [RelayCommand]
    private void EditarCarpeta(CarpetaViewModel? carpeta)
    {
        if (carpeta is null)
            return;

        var original = carpeta.Carpeta;

        AbrirModal(
            "Editar carpeta",
            "Cambia el nombre, la descripción o el color.",
            carpeta.Nombre,
            true,
            false,
            async () =>
            {
                var nombre = ModalValor.Trim();

                if (nombre.Length == 0)
                    return;

                var descripcion = ModalDescripcionCarpeta.Trim();
                var colorId = ModalColorId;

                if (nombre == original.Nombre
                    && descripcion == original.Descripcion
                    && colorId == original.ColorId)
                {
                    return;
                }

                await _almacenamiento.GuardarCarpetaAsync(original with
                {
                    Nombre = nombre,
                    Descripcion = descripcion,
                    ColorId = colorId
                });

                await CargarDatosAsync();
                Estado = $"Carpeta «{nombre}» actualizada.";
            },
            textoBoton: "Guardar",
            mostrarColores: true,
            descripcionCarpeta: original.Descripcion,
            colorId: original.ColorId);
    }

    [RelayCommand]
    private void EliminarCarpeta(CarpetaViewModel? carpeta)
    {
        if (carpeta is null)
            return;

        var id = carpeta.Id;

        AbrirModal(
            "Eliminar carpeta",
            $"Se eliminará la carpeta «{carpeta.Nombre}» junto con todos sus exámenes y el progreso asociado. No se puede deshacer.",
            string.Empty, false, true, async () =>
            {
                await _almacenamiento.EliminarCarpetaAsync(id);
                await CargarDatosAsync();
                Estado = "Carpeta eliminada.";
            },
            textoBoton: "Eliminar");
    }

    // ─────────────── Exámenes ───────────────

    [RelayCommand]
    private void RenombrarTest(TestViewModel? test)
    {
        if (test is null)
            return;

        var original = test.Test;

        AbrirModal("Renombrar examen", "Escribe el nuevo nombre.", test.Titulo, true, false, async () =>
        {
            var titulo = ModalValor.Trim();

            if (titulo.Length == 0 || titulo == original.Titulo)
                return;

            await _almacenamiento.GuardarTestAsync(original with { Titulo = titulo });
            await CargarDatosAsync();
            Estado = $"Examen renombrado a «{titulo}».";
        });
    }

    [RelayCommand]
    private void EliminarTest(TestViewModel? test)
    {
        if (test is null)
            return;

        var id = test.Id;

        AbrirModal(
            "Eliminar examen",
            $"Se eliminará «{test.Titulo}» y su progreso. No se puede deshacer.",
            string.Empty, false, true, async () =>
            {
                await _almacenamiento.EliminarTestAsync(id);
                await CargarDatosAsync();
                Estado = "Examen eliminado.";
            },
            textoBoton: "Eliminar");
    }

    // ─────────────── Práctica combinada ───────────────

    /// <summary>Arranca una práctica con las preguntas de varios exámenes a la vez.</summary>
    public void PracticarSeleccion(IReadOnlyList<TestViewModel> tests)
    {
        var preguntas = tests.SelectMany(t => t.Preguntas).ToList();

        if (preguntas.Count == 0)
            return;

        VistaActual = CrearQuiz("Práctica combinada", preguntas);
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
        return new QuizViewModel(
            titulo,
            preguntas,
            (preguntaId, acertada) => RegistrarResultadoAsync(preguntaId, acertada, vistaAnterior),
            async () =>
            {
                VistaActual = vistaAnterior;
                await CargarDatosAsync();
            });
    }

    private async Task RegistrarResultadoAsync(string preguntaId, bool acertada, object? vistaAnterior)
    {
        var progreso = new ProgresoPregunta(
            preguntaId,
            acertada,
            acertada ? 0 : 1,
            DateTimeOffset.Now);

        var idsSeleccionados = (vistaAnterior as FolderViewModel)?.IdsSeleccionados;

        await _almacenamiento.GuardarProgresoAsync(progreso);
        await CargarDatosAsync();

        if (vistaAnterior is FolderViewModel carpetaAbierta)
            carpetaAbierta.Refrescar(idsSeleccionados);
    }

    [RelayCommand]
    private void SeleccionarColorModal(string? colorId)
    {
        if (!string.IsNullOrWhiteSpace(colorId))
            ModalColorId = colorId;
    }

    [RelayCommand]
    private void AbrirModalCarpeta()
    {
        AbrirModal(
            "Nueva carpeta",
            "Ponle un nombre a la carpeta y elige su color.",
            string.Empty,
            true,
            false,
            CrearCarpeta,
            textoBoton: "Crear carpeta",
            mostrarColores: true);
    }
}
