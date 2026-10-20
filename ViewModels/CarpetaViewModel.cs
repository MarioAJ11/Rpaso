using Avalonia.Media;
using Rpaso.Models;

namespace Rpaso.ViewModels;

/// <summary>
/// Presentación de una carpeta temática:	color, número de tests y de preguntas falladas.
/// </summary>
public sealed class CarpetaViewModel : ViewModelBase
{
    public CarpetaViewModel(Carpeta carpeta, IReadOnlyList<Test> tests, IReadOnlyDictionary<string, ProgresoPregunta> progreso)
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
    }

    public Carpeta Carpeta { get; }

    public string Id => Carpeta.Id;

    public string Nombre => Carpeta.Nombre;

    public string Descripcion => string.IsNullOrWhiteSpace(Carpeta.Descripcion)
        ? "Sin descripción"
        : Carpeta.Descripcion;

    public IBrush Fondo { get; }

    public IBrush Borde { get; }

    public IBrush Texto { get; }

    public IBrush Icono { get; }

    public IReadOnlyList<TestViewModel> Tests { get; }

    public int TotalFalladas => Tests.Sum(t => t.TotalFalladas);

    public bool HayFalladas => TotalFalladas > 0;

    public int TotalPreguntas => Tests.Sum(t => t.TotalPreguntas);

    public int NumeroPdfs => Tests.Count;
}
