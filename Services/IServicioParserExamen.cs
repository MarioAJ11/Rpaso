using Rpaso.Models;

namespace Rpaso.Services;

/// <summary>
/// Resultado de analizar un PDF: el test obtenido y, si no se pudo extraer nada,
/// el motivo concreto.
/// </summary>
public sealed record AnalisisExamen(Test Test, string? Aviso, int Descartadas = 0)
{
    /// <summary>True si se han reconocido preguntas con sus opciones.</summary>
    public bool Correcto => Test.Preguntas.Count > 0;
}

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
    Task<AnalisisExamen> AnalizarAsync(string rutaPdf, string carpetaId, CancellationToken ct = default);
}
