using Avalonia.Media;
using Rpaso.Models;

namespace Rpaso.ViewModels;

/// <summary>
/// Presentación de una carpeta temática: color, número de tests y de preguntas falladas.
/// </summary>
public sealed class CarpetaViewModel : ViewModelBase
{
    public CarpetaViewModel(Carpeta carpeta, IReadOnlyList<Test> tests, IReadOnlyDictionary<string, ProgresoPregunta> progreso)
        => Actualizar(carpeta, tests, progreso);

    public Carpeta Carpeta { get; private set; } = null!;

    public string Id => Carpeta.Id;

    public string Nombre => Carpeta.Nombre;

    public string Descripcion => string.IsNullOrWhiteSpace(Carpeta.Descripcion)
        ? "Sin descripción"
        : Carpeta.Descripcion;

    public IBrush Fondo { get; private set; } = null!;

    public IBrush Borde { get; private set; } = null!;

    public IBrush Texto { get; private set; } = null!;

    public IBrush Icono { get; private set; } = null!;

    public IReadOnlyList<TestViewModel> Tests { get; private set; } = [];

    public int TotalFalladas => Tests.Sum(t => t.TotalFalladas);

    public bool HayFalladas => TotalFalladas > 0;

    public int TotalPreguntas => Tests.Sum(t => t.TotalPreguntas);

    public int NumeroPdfs => Tests.Count;

    /// <summary>
    /// Vuelca los datos del almacenamiento sobre la misma instancia, para que las
    /// vistas ya abiertas se actualicen sin perder la posición del usuario.
    /// </summary>
    public void Actualizar(Carpeta carpeta, IReadOnlyList<Test> tests, IReadOnlyDictionary<string, ProgresoPregunta> progreso)
    {
        Carpeta = carpeta;

        var color = PaletaColores.Obtener(carpeta.ColorId);
        Fondo = Brush.Parse(color.Fondo);
        Borde = Brush.Parse(color.Borde);
        Texto = Brush.Parse(color.Texto);
        Icono = Brush.Parse(color.Icono);

        Tests = tests
            .Where(t => t.CarpetaId == carpeta.Id)
            .Select(t => new TestViewModel(t, progreso))
            .ToList();

        OnPropertyChanged(nameof(Carpeta));
        OnPropertyChanged(nameof(Id));
        OnPropertyChanged(nameof(Nombre));
        OnPropertyChanged(nameof(Descripcion));
        OnPropertyChanged(nameof(Fondo));
        OnPropertyChanged(nameof(Borde));
        OnPropertyChanged(nameof(Texto));
        OnPropertyChanged(nameof(Icono));
        OnPropertyChanged(nameof(Tests));
        OnPropertyChanged(nameof(TotalFalladas));
        OnPropertyChanged(nameof(HayFalladas));
        OnPropertyChanged(nameof(TotalPreguntas));
        OnPropertyChanged(nameof(NumeroPdfs));
    }
}
