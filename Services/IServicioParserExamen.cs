using Rpaso.Models;

namespace Rpaso.Services;

/// <summary>
/// Convierte un PDF de examen en preguntas usando únicamente la geometría del texto.
/// </summary>
public interface IServicioParserExamen
{
    /// <summary>
    /// Analiza el PDF y devuelve el test con sus preguntas y claves de respuesta.
    /// </summary>
    /// <param name="rutaPdf">Ruta completa del PDF.</param>
    /// <param name="carpetaId">Carpeta destino. Si es vacía, el test queda sin carpeta.</param>
    Task<Test> AnalizarAsync(string rutaPdf, string carpetaId, CancellationToken ct = default);
}
