using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Rpaso.ViewModels;

namespace Rpaso.Views;

public partial class FolderView : UserControl
{
    public FolderView()
    {
        InitializeComponent();
    }

    private void OnTestListSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        var panel = ListaTests.GetVisualDescendants().OfType<WrapPanel>().FirstOrDefault();
        if (panel is null || e.NewSize.Width <= 0)
            return;

        const double anchoMinimoTarjeta = 360;
        const double espacio = 20;
        var columnas = e.NewSize.Width >= anchoMinimoTarjeta * 3 + espacio * 2
            ? 3
            : e.NewSize.Width >= anchoMinimoTarjeta * 2 + espacio
                ? 2
                : 1;

        panel.ItemWidth = (e.NewSize.Width - espacio * (columnas - 1)) / columnas;
    }

    private void OnTestTitleDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Control { DataContext: TestViewModel test }
            || DataContext is not FolderViewModel folder)
            return;

        folder.RenombrarTestCommand.Execute(test);
        e.Handled = true;
    }

    private void OnTestContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (sender is not Border { DataContext: TestViewModel test } border
            || DataContext is not FolderViewModel folder)
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

        menu.Items.Add(CrearElementoMenu("Editar", folder.EditarTestCommand, test, texto));
        menu.Items.Add(CrearElementoMenu("Eliminar", folder.EliminarTestCommand, test, texto));

        menu.Open(border);
        e.Handled = true;
    }

    private static MenuItem CrearElementoMenu(
        string texto,
        System.Windows.Input.ICommand comando,
        TestViewModel test,
        IBrush pincelTexto)
        => new()
        {
            Header = new TextBlock { Text = texto, Foreground = pincelTexto },
            Foreground = pincelTexto,
            Command = comando,
            CommandParameter = test,
            Padding = new Avalonia.Thickness(12, 8),
            MinHeight = 34
        };
}
