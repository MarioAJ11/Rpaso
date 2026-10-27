using Rpaso.Models;

namespace Rpaso.Services;

/// <summary>
/// Caso de uso de importación: PDF -> preguntas -> test guardado.
/// </summary>
public interface IServicioImportacion
{
    /// <summary>
    /// Analiza el PDF, extrae sus preguntas y guarda el test resultante.
    /// </summary>
    /// <param name="rutaPdf">Ruta completa del archivo PDF.</param>
    /// <param name="carpetaId">Carpeta destino. Si es vacía, el test queda sin carpeta.</param>
    Task<Test> ImportarAsync(string rutaPdf, string carpetaId, CancellationToken ct = default);

    /// <summary>
    /// Importa varios PDFs. Un archivo que falla no detiene el resto: se registra el motivo
    /// y se sigue con el siguiente.
    /// </summary>
    /// <param name="rutas">Rutas de los archivos a importar.</param>
    /// <param name="carpetaId">Carpeta destino.</param>
    /// <param name="progreso">Notifica el archivo que se está procesando.</param>
    /// <param name="ct">Permite cancelar la importación.</param>
    Task<InformeImportacion> ImportarVariosAsync(
        IEnumerable<string> rutas,
        string carpetaId,
        IProgress<string>? progreso = null,
        CancellationToken ct = default);
}

/// <inheritdoc cref="IServicioImportacion"/>
public sealed class ServicioImportacion : IServicioImportacion
{
    private readonly IServicioParserExamen _parser;
    private readonly IServicioAlmacenamiento _almacenamiento;

    public ServicioImportacion(IServicioParserExamen parser, IServicioAlmacenamiento almacenamiento)
    {
        _parser = parser;
        _almacenamiento = almacenamiento;
    }

    public async Task<Test> ImportarAsync(string rutaPdf, string carpetaId, CancellationToken ct = default)
    {
        var analisis = await _parser.AnalizarAsync(rutaPdf, carpetaId, ct);

        if (!analisis.Correcto)
            throw new InvalidOperationException(analisis.Aviso ?? "No se han encontrado preguntas.");

        await _almacenamiento.GuardarTestAsync(analisis.Test, ct);

        return analisis.Test;
    }

    public async Task<InformeImportacion> ImportarVariosAsync(
        IEnumerable<string> rutas,
        string carpetaId,
        IProgress<string>? progreso = null,
        CancellationToken ct = default)
    {
        var resultados = new List<ResultadoImportacion>();

        foreach (var ruta in rutas)
        {
            ct.ThrowIfCancellationRequested();
            progreso?.Report(Path.GetFileName(ruta));

            try
            {
                var analisis = await _parser.AnalizarAsync(ruta, carpetaId, ct);

                if (!analisis.Correcto)
                {
                    resultados.Add(ResultadoImportacion.Fallido(ruta, analisis.Aviso ?? "No se han encontrado preguntas."));
                    continue;
                }

                await _almacenamiento.GuardarTestAsync(analisis.Test, ct);
                resultados.Add(ResultadoImportacion.Importado(
                    ruta, analisis.Test.Preguntas.Count, analisis.Descartadas));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                resultados.Add(ResultadoImportacion.Fallido(ruta, ex.Message));
            }
        }

        return new InformeImportacion(resultados);
    }
}
