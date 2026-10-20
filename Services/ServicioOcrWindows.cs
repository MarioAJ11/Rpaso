using Windows.Data.Pdf;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Rpaso.Services;

/// <summary>Página recognised por OCR, con las palabras y su posición.</summary>
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
/// No añade ningún peso al ejecutable: ya viene en Windows.
/// </summary>
public sealed class ServicioOcrWindows : IServicioOcr
{
    /// <summary>Ancho en píxeles al que se renderiza la página. Más píxeles = mejor OCR.</summary>
    private const int AnchoRender = 2000;

    public bool Disponible { get; }

    public string? MotivoIndisponible { get; }

    public ServicioOcrWindows()
    {
        try
        {
            var motor = OcrEngine.TryCreateFromUserProfileLanguages();

            if (motor is null)
            {
                MotivoIndisponible =
                    "Windows no tiene ningún motor de OCR para los idiomas del sistema. "
                    + "Instala el paquete de idioma en Configuración > Hora e idioma > Idioma y región.";
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

    public async Task<IReadOnlyList<PaginaOcr>> ReconocerAsync(string rutaPdf, CancellationToken ct = default)
    {
        var motor = OcrEngine.TryCreateFromUserProfileLanguages()
            ?? throw new InvalidOperationException(MotivoIndisponible ?? "No hay motor de OCR disponible.");

        var paginas = new List<PaginaOcr>();

        // Se usa el Stream de .NET en vez de StorageFile: en aplicaciones de escritorio
        // sin empaquetar, GetFileFromPathAsync falla con rutas locales.
        using var archivo = File.OpenRead(rutaPdf);
        using var flujo = archivo.AsRandomAccessStream();

        var pdf = await PdfDocument.LoadFromStreamAsync(flujo);

        for (uint i = 0; i < pdf.PageCount; i++)
        {
            ct.ThrowIfCancellationRequested();

            var pagina = pdf.GetPage(i);

            try
            {
                var anchoPuntos = pagina.Size.Width;
                var altoPuntos = pagina.Size.Height;

                using var stream = new InMemoryRandomAccessStream();
                await pagina.RenderToStreamAsync(stream);
                stream.Seek(0);

                var decodificador = await BitmapDecoder.CreateAsync(stream);

                // El OCR tiene un límite de tamaño: si la página es muy grande se re-renderiza
                // escalada. El factor sirve para pasar de píxeles a puntos del PDF.
                var anchoRender = decodificador.PixelWidth;

                if (anchoRender > OcrEngine.MaxImageDimension)
                {
                    var escalaDestino = OcrEngine.MaxImageDimension / (double)anchoRender;

                    using var streamEscalado = new InMemoryRandomAccessStream();

                    await pagina.RenderToStreamAsync(streamEscalado, new PdfPageRenderOptions
                    {
                        DestinationWidth = (uint)OcrEngine.MaxImageDimension
                    });

                    streamEscalado.Seek(0);

                    var decodificadorEscalado = await BitmapDecoder.CreateAsync(streamEscalado);

                    var bitmapEscalado = await decodificadorEscalado.GetSoftwareBitmapAsync(
                        BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

                    using (bitmapEscalado)
                    {
                        paginas.Add(ReconocerPagina(motor, bitmapEscalado, anchoPuntos, altoPuntos, escalaDestino));
                    }

                    continue;
                }

                var bitmap = await decodificador.GetSoftwareBitmapAsync(
                    BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

                using (bitmap)
                {
                    var escala = anchoPuntos > 0 ? anchoRender / anchoPuntos : 1;
                    paginas.Add(ReconocerPagina(motor, bitmap, anchoPuntos, altoPuntos, escala));
                }
            }
            finally
            {
                pagina.Dispose();
            }
        }

        return paginas;
    }

    /// <summary>
    /// Reconoce una página ya renderizada y devuelve sus palabras con la posición convertida
    /// a puntos del PDF (Y hacia arriba, como en PdfPig).
    /// </summary>
    private static PaginaOcr ReconocerPagina(
        OcrEngine motor,
        SoftwareBitmap bitmap,
        double anchoPuntos,
        double altoPuntos,
        double escala)
    {
        var resultado = motor.RecognizeAsync(bitmap).AsTask().GetAwaiter().GetResult();

        var palabras = new List<PalabraTexto>();

        foreach (var linea in resultado.Lines)
        {
            foreach (var palabra in linea.Words)
            {
                var rect = palabra.BoundingRect;

                // El OCR mide Y hacia abajo; el PDF, hacia arriba.
                var superior = altoPuntos - (rect.Y / escala);
                var inferior = altoPuntos - ((rect.Y + rect.Height) / escala);

                palabras.Add(new PalabraTexto(
                    palabra.Text,
                    rect.X / escala,
                    (rect.X + rect.Width) / escala,
                    superior,
                    inferior));
            }
        }

        return new PaginaOcr(palabras, anchoPuntos, altoPuntos);
    }
}
