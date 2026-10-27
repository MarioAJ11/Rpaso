using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Rpaso.ViewModels;

namespace Rpaso.Views;

public partial class HomeView : UserControl
{
    public HomeView()
    {
        InitializeComponent();
    }

    private void OnFolderContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (sender is not Button { DataContext: CarpetaViewModel carpeta } button
            || DataContext is not HomeViewModel viewModel)
            return;

        var fondo = Brush.Parse("#F1F3F5");
        var texto = Brush.Parse("#334155");
        var menu = new ContextMenu
        {
            Background = fondo,
            Foreground = texto,
            BorderBrush = Brush.Parse("#D1D5DB"),
            BorderThickness = new Avalonia.Thickness(1),
            Padding = new Avalonia.Thickness(5),
            MinWidth = 150
        };

        menu.Items.Add(CrearElementoMenu("Editar", viewModel.EditarCarpetaCommand, carpeta, texto));
        menu.Items.Add(CrearElementoMenu("Eliminar", viewModel.EliminarCarpetaCommand, carpeta, texto));

        menu.Open(button);
        e.Handled = true;
    }

    private static MenuItem CrearElementoMenu(
        string texto,
        System.Windows.Input.ICommand comando,
        CarpetaViewModel carpeta,
        IBrush pincelTexto)
        => new()
        {
            Header = new TextBlock { Text = texto, Foreground = pincelTexto },
            Foreground = pincelTexto,
            Command = comando,
            CommandParameter = carpeta,
            Padding = new Avalonia.Thickness(12, 8),
            MinHeight = 34
        };
}
