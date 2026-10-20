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
                new ServicioExtraccionPdf(),
                new ServicioEstructuracionIa(),
                almacenamiento);

            var viewModel = new MainViewModel(
                almacenamiento,
                importacion,
                () => SeleccionarPdfAsync(ventana));

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

        if (string.IsNullOrEmpty(vista))
            return;

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
    private static async Task<string?> SeleccionarPdfAsync(Window ventana)
    {
        var archivos = await ventana.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Selecciona un PDF de examen",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Documentos PDF") { Patterns = ["*.pdf"] }
            ]
        });

        return archivos.Count == 0 ? null : archivos[0].TryGetLocalPath();
    }
}
