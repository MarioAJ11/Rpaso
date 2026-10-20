using Rpaso.Models;

namespace Rpaso.Services;

/// <summary>
/// Caso de uso de importación: PDF -> preguntas -> test guardado.
/// </summary>
public interface IServicioImportacion
{
    /// <summary>
    /// Extrae el texto del PDF, lo estructura con el modelo local y guarda el test resultante.
    /// </summary>
    /// <param name="rutaPdf">Ruta completa del archivo PDF.</param>
    /// <param name="carpetaId">Carpeta destino. Si es vacía, el test queda sin carpeta.</param>
    Task<Test> ImportarAsync(string rutaPdf, string carpetaId, CancellationToken ct = default);
}

/// <inheritdoc cref="IServicioImportacion"/>
public sealed class ServicioImportacion : IServicioImportacion
{
    private readonly IServicioExtraccionPdf _extraccion;
    private readonly IServicioEstructuracionIa _estructuracion;
    private readonly IServicioAlmacenamiento _almacenamiento;

    public ServicioImportacion(
        IServicioExtraccionPdf extraccion,
        IServicioEstructuracionIa estructuracion,
        IServicioAlmacenamiento almacenamiento)
    {
        _extraccion = extraccion;
        _estructuracion = estructuracion;
        _almacenamiento = almacenamiento;
    }

    public async Task<Test> ImportarAsync(string rutaPdf, string carpetaId, CancellationToken ct = default)
    {
        var paginas = await _extraccion.ExtraerPaginasAsync(rutaPdf, ct);
        var preguntas = await _estructuracion.EstructurarAsync(paginas, ct);

        var test = new Test(
            Id: $"t-{Guid.NewGuid():N}"[..12],
            Titulo: Path.GetFileNameWithoutExtension(rutaPdf),
            CarpetaId: carpetaId,
            FechaImportacion: DateTimeOffset.Now,
            ArchivoOrigen: Path.GetFileName(rutaPdf),
            Preguntas: preguntas);

        await _almacenamiento.GuardarTestAsync(test, ct);

        return test;
    }
}
