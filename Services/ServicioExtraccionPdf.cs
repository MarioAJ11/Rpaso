using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace Rpaso.Services;

/// <summary>
/// Extracción de texto con PdfPig.
/// </summary>
public sealed class ServicioExtraccionPdf : IServicioExtraccionPdf
{
    /// <summary>
    /// Opciones del extractor de orden de contenido. Se preservan los saltos de línea (son
    /// la frontera entre enunciado y opciones) y se colapsan los espacios runs de un PDF,
    /// que suelen traer caracteres sueltos del maquetador.
    /// </summary>
    private static readonly ContentOrderTextExtractor.Options OpcionesTexto = new()
    {
        ReplaceWhitespaceWithSpace = true,
        SeparateParagraphsWithDoubleNewline = true,
        NegativeGapAsWhitespace = true
    };

    public Task<IReadOnlyList<string>> ExtraerPaginasAsync(string rutaPdf, CancellationToken ct = default)
        // PdfPig es síncrono y pesado: se ejecuta en un hilo de fondo para no congelar la UI.
        => Task.Run(() => ExtraerPaginas(rutaPdf, ct), ct);

    private static IReadOnlyList<string> ExtraerPaginas(string rutaPdf, CancellationToken ct)
    {
        if (!File.Exists(rutaPdf))
            throw new FileNotFoundException("No se encontró el archivo PDF.", rutaPdf);

        var paginas = new List<string>();

        using var documento = PdfDocument.Open(rutaPdf);

        foreach (var pagina in documento.GetPages())
        {
            ct.ThrowIfCancellationRequested();

            var texto = ContentOrderTextExtractor.GetText(pagina, OpcionesTexto);

            if (!string.IsNullOrWhiteSpace(texto))
                paginas.Add(texto.Trim());
        }

        return paginas;
    }
}
