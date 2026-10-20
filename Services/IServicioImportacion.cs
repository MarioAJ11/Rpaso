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
        var test = await _parser.AnalizarAsync(rutaPdf, carpetaId, ct);

        await _almacenamiento.GuardarTestAsync(test, ct);

        return test;
    }
}
