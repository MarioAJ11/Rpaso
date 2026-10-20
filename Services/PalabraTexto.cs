namespace Rpaso.Services;

/// <summary>
/// Palabra con su posición en la página. Abstracción sobre el origen del texto:
/// viene del PDF (PdfPig) o del OCR (Windows.Media.Ocr), de modo que el parser
/// geométrico es el mismo en ambos casos.
/// </summary>
/// <param name="Texto">Contenido de la palabra.</param>
/// <param name="Izquierda">X del borde izquierdo.</param>
/// <param name="Derecha">X del borde derecho.</param>
/// <param name="Superior">Y del borde superior (crece hacia arriba, como en PDF).</param>
/// <param name="Inferior">Y del borde inferior.</param>
public sealed record PalabraTexto(
    string Texto,
    double Izquierda,
    double Derecha,
    double Superior,
    double Inferior,
    bool Negrita = false)
{
    /// <summary>Altura del glifo.</summary>
    public double Alto => Superior - Inferior;

    /// <summary>Crea la palabra a partir de una esquina y sus dimensiones.</summary>
    public static PalabraTexto DesdeRectangulo(string texto, double x, double y, double ancho, double alto)
        => new(texto, x, x + ancho, y + alto, y);
}
