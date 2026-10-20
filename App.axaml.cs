using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Rpaso.Services;
using Rpaso.ViewModels;
using Rpaso.Views;

namespace Rpaso;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var ventana = new MainWindow();

            var almacenamiento = new ServicioAlmacenamiento();
            var importacion = new ServicioImportacion(
                new ServicioParserExamen(),
                almacenamiento);

            var viewModel = new MainViewModel(
                almacenamiento,
                importacion,
                () => SeleccionarPdfsAsync(ventana));

            ventana.DataContext = viewModel;
            desktop.MainWindow = ventana;

            _ = viewModel.CargarDatosAsync();
            _ = IniciarEnVistaDePruebaAsync(viewModel);
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Arranca directamente en una vista concreta (carpeta o quiz). Solo para depuración:
    /// se activa con la variable de entorno RPASO_TEST_VISTA.
    /// </summary>
    private static async Task IniciarEnVistaDePruebaAsync(MainViewModel viewModel)
    {
        var vista = Environment.GetEnvironmentVariable("RPASO_TEST_VISTA");

        if (vista is not ("carpeta" or "quiz" or "quiz-corregido" or "informe"))
            return;

        // Modo informe: importa los PDFs de RPASO_TEST_PDFS (separados por ';') y muestra el informe.
        if (vista == "informe")
        {
            var rutas = (Environment.GetEnvironmentVariable("RPASO_TEST_PDFS") ?? string.Empty)
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (rutas.Length > 0)
                await viewModel.ImportarVariosAsync(rutas);

            return;
        }

        await viewModel.CargarDatosAsync();

        if (viewModel.Carpetas.Count == 0)
            return;

        var carpeta = viewModel.Carpetas[0];
        viewModel.AbrirCarpetaCommand.Execute(carpeta);

        if (vista is "quiz" or "quiz-corregido")
        {
            viewModel.PracticarCarpetaCommand.Execute(carpeta);

            if (vista == "quiz-corregido" && viewModel.VistaActual is QuizViewModel quiz)
            {
                quiz.SeleccionarCommand.Execute("a");
                quiz.ComprobarCommand.Execute(null);
            }
        }
    }

    /// <summary>
    /// Abre el diálogo de selección de archivos. Vive en la capa de presentación porque
    /// necesita el StorageProvider de la ventana; el ViewModel solo recibe una función.
    /// </summary>
    private static async Task<IReadOnlyList<string>> SeleccionarPdfsAsync(Window ventana)
    {
        var archivos = await ventana.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Selecciona uno o varios PDF de examen",
            AllowMultiple = true,
            FileTypeFilter =
            [
                new FilePickerFileType("Documentos PDF") { Patterns = ["*.pdf"] }
            ]
        });

        return archivos
            .Select(archivo => archivo.TryGetLocalPath())
            .Where(ruta => !string.IsNullOrWhiteSpace(ruta))
            .Select(ruta => ruta!)
            .ToList();
    }
}
