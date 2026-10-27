using System.Text;
using Windows.Data.Pdf;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace Rpaso.Services;

/// <summary>Página reconocida por OCR, con las palabras y su posición.</summary>
public sealed record PaginaOcr(IReadOnlyList<PalabraTexto> Palabras, double Ancho, double Alto);

/// <summary>
/// Reconocimiento óptico de caracteres para PDF escaneados.
/// </summary>
public interface IServicioOcr
{
    /// <summary>True si hay un motor de OCR disponible en el sistema.</summary>
    bool Disponible { get; }

    /// <summary>Motivo por el que no se puede usar el OCR, o null si está disponible.</summary>
    string? MotivoIndisponible { get; }

    /// <summary>Renderiza las páginas del PDF y devuelve el texto con sus coordenadas.</summary>
    Task<IReadOnlyList<PaginaOcr>> ReconocerAsync(string rutaPdf, CancellationToken ct = default);
}

/// <summary>
/// OCR con las APIs de Windows (Windows.Data.Pdf + Windows.Media.Ocr).
/// </summary>
public sealed class ServicioOcrWindows : IServicioOcr
{
    /// <summary>Ancho en píxeles (~300 DPI para A4). Evita que el OCR confunda 'l' con '1' o pierda puntos.</summary>
    private const int AnchoRender = 2400;

    public bool Disponible { get; }

    public string? MotivoIndisponible { get; }

    public ServicioOcrWindows()
    {
        try
        {
            var motor = CrearMotorPreferente();

            if (motor is null)
            {
                MotivoIndisponible =
                    "Windows no tiene ningún motor de OCR para español ni para los idiomas del sistema. "
                    + "Instala el paquete de idioma Español en Configuración > Hora e idioma > Idioma y región.";
            }
            else
            {
                Disponible = true;
            }
        }
        catch (Exception ex)
        {
            MotivoIndisponible = $"No se pudo inicializar el OCR: {ex.Message}";
        }
    }

    private static OcrEngine? CrearMotorPreferente()
    {
        // Priorizar español explícitamente para garantizar reconocimiento de ñ, tildes y signos ¿?
        string[] idiomasPreferidos = ["es-ES", "es-MX", "es"];

        foreach (var codigo in idiomasPreferidos)
        {
            try
            {
                var lang = new Language(codigo);
                if (OcrEngine.IsLanguageSupported(lang))
                    return OcrEngine.TryCreateFromLanguage(lang);
            }
            catch
            {
                // Ignorar si el código de cultura falla en una versión específica de Windows
            }
        }

        return OcrEngine.TryCreateFromUserProfileLanguages();
    }

    public async Task<IReadOnlyList<PaginaOcr>> ReconocerAsync(string rutaPdf, CancellationToken ct = default)
    {
        var motor = CrearMotorPreferente()
            ?? throw new InvalidOperationException(MotivoIndisponible ?? "No hay motor de OCR disponible.");

        var paginas = new List<PaginaOcr>();

        using var archivo = File.OpenRead(rutaPdf);
        using var flujo = archivo.AsRandomAccessStream();

        var pdf = await PdfDocument.LoadFromStreamAsync(flujo);

        for (uint i = 0; i < pdf.PageCount; i++)
        {
            ct.ThrowIfCancellationRequested();

            using var pagina = pdf.GetPage(i);

            var anchoPuntos = pagina.Size.Width;
            var altoPuntos = pagina.Size.Height;

            var factorEscala = anchoPuntos > 0 ? AnchoRender / anchoPuntos : 2.5;
            var anchoDestino = (int)Math.Min(AnchoRender, OcrEngine.MaxImageDimension);
            var altoDestino = (int)Math.Min(altoPuntos * factorEscala, OcrEngine.MaxImageDimension);

            // Si la página es muy alta y toca el techo de MaxImageDimension, reajustamos el ancho proporcionalmente
            if (altoPuntos > 0 && altoDestino < altoPuntos * factorEscala)
            {
                anchoDestino = (int)Math.Max(1, anchoPuntos * (altoDestino / altoPuntos));
            }

            using var stream = new InMemoryRandomAccessStream();

            await pagina.RenderToStreamAsync(stream, new PdfPageRenderOptions
            {
                DestinationWidth = (uint)Math.Max(1, anchoDestino),
                DestinationHeight = (uint)Math.Max(1, altoDestino)
            });

            stream.Seek(0);

            var decodificador = await BitmapDecoder.CreateAsync(stream);

            using var bitmap = await decodificador.GetSoftwareBitmapAsync(
                BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

            var escalaX = anchoPuntos > 0 ? bitmap.PixelWidth / anchoPuntos : 1.0;
            var escalaY = altoPuntos > 0 ? bitmap.PixelHeight / altoPuntos : 1.0;

            var paginaReconocida = await ReconocerPaginaAsync(motor, bitmap, anchoPuntos, altoPuntos, escalaX, escalaY);
            paginas.Add(paginaReconocida);
        }

        return paginas;
    }

    private static async Task<PaginaOcr> ReconocerPaginaAsync(
        OcrEngine motor,
        SoftwareBitmap bitmap,
        double anchoPuntos,
        double altoPuntos,
        double escalaX,
        double escalaY)
    {
        var resultado = await motor.RecognizeAsync(bitmap);
        var palabras = new List<PalabraTexto>();

        foreach (var linea in resultado.Lines)
        {
            foreach (var palabra in linea.Words)
            {
                var textoLimpio = palabra.Text.Normalize(NormalizationForm.FormC).Trim();
                if (string.IsNullOrWhiteSpace(textoLimpio))
                    continue;

                var rect = palabra.BoundingRect;

                // El OCR mide Y hacia abajo; el PDF, hacia arriba.
                var superior = altoPuntos - (rect.Y / escalaY);
                var inferior = altoPuntos - ((rect.Y + rect.Height) / escalaY);

                palabras.Add(new PalabraTexto(
                    textoLimpio,
                    rect.X / escalaX,
                    (rect.X + rect.Width) / escalaX,
                    superior,
                    inferior));
            }
        }

        return new PaginaOcr(palabras, anchoPuntos, altoPuntos);
    }
}