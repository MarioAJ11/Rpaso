using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Rpaso.Models;

namespace Rpaso.ViewModels;

/// <summary>
/// Un color de la paleta, con su estado de selección para la interfaz.
/// </summary>
public sealed partial class ColorCarpetaViewModel : ViewModelBase
{
    public ColorCarpetaViewModel(ColorCarpeta color)
    {
        Color = color;
        Fondo = Brush.Parse(color.Fondo);
        Borde = Brush.Parse(color.Borde);
        Texto = Brush.Parse(color.Texto);
        Icono = Brush.Parse(color.Icono);
    }

    public ColorCarpeta Color { get; }

    public string Id => Color.Id;

    public string Nombre => Color.Nombre;

    public IBrush Fondo { get; }

    public IBrush Borde { get; }

    public IBrush Texto { get; }

    public IBrush Icono { get; }

    [ObservableProperty]
    private bool _seleccionado;
}
