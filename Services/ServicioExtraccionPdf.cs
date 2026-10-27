using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace Rpaso.Services;

/// <summary>
/// Extracción de texto con PdfPig normalizada y sin cortes de guion.
/// </summary>
public sealed class ServicioExtraccionPdf : IServicioExtraccionPdf
{
    private static readonly ContentOrderTextExtractor.Options OpcionesTexto = new()
    {
        ReplaceWhitespaceWithSpace = true,
        SeparateParagraphsWithDoubleNewline = true,
        NegativeGapAsWhitespace = true
    };

    private static readonly Regex RegexGuionSaltoLinea = new(
        @"(\p{L}+)-\r?\n\s*(\p{L}+)",
        RegexOptions.Compiled);

    public Task<IReadOnlyList<string>> ExtraerPaginasAsync(string rutaPdf, CancellationToken ct = default)
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
            {
                var limpio = NormalizarTextoPdf(texto);
                if (!string.IsNullOrWhiteSpace(limpio))
                    paginas.Add(limpio);
            }
        }

        return paginas;
    }

    public static string NormalizarTextoPdf(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return string.Empty;

        // 1. Unificar acentos descompuestos (ej: 'a' + '´' -> 'á')
        var resultado = texto.Normalize(NormalizationForm.FormC);

        // 2. Sustituir ligaduras tipográficas habituales en PDF y espacios duros
        resultado = resultado
            .Replace("\uFB00", "ff")
            .Replace("\uFB01", "fi")
            .Replace("\uFB02", "fl")
            .Replace("\uFB03", "ffi")
            .Replace("\uFB04", "ffl")
            .Replace('\u00A0', ' ')
            .Replace('\u200B', ' ')
            .Replace("“", "\"")
            .Replace("”", "\"")
            .Replace("‘", "'")
            .Replace("’", "'");

        // 3. Unir palabras partidas por guion al final de línea
        resultado = RegexGuionSaltoLinea.Replace(resultado, "$1$2");

        return resultado.Trim();
    }
}