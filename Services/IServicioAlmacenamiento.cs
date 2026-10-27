using Rpaso.Models;

namespace Rpaso.Services;

/// <summary>
/// Contrato de persistencia de la aplicación.
/// </summary>
public interface IServicioAlmacenamiento
{
    /// <summary>Carga las carpetas temáticas. Devuelve lista vacía si no hay datos.</summary>
    Task<IReadOnlyList<Carpeta>> CargarCarpetasAsync(CancellationToken ct = default);

    /// <summary>Añade una carpeta o reemplaza la existente con el mismo Id.</summary>
    Task GuardarCarpetaAsync(Carpeta carpeta, CancellationToken ct = default);

    /// <summary>Elimina una carpeta, sus tests y el progreso de sus preguntas.</summary>
    Task EliminarCarpetaAsync(string carpetaId, CancellationToken ct = default);

    /// <summary>Elimina un test.</summary>
    Task EliminarTestAsync(string testId, CancellationToken ct = default);

    /// <summary>Carga todos los tests importados. Devuelve lista vacía si no hay datos.</summary>
    Task<IReadOnlyList<Test>> CargarTestsAsync(CancellationToken ct = default);

    /// <summary>Añade un test o reemplaza el existente con el mismo Id.</summary>
    Task GuardarTestAsync(Test test, CancellationToken ct = default);

    /// <summary>Carga el progreso del usuario sobre las preguntas ya respondidas.</summary>
    Task<IReadOnlyList<ProgresoPregunta>> CargarProgresoAsync(CancellationToken ct = default);

    /// <summary>Guarda el progreso de una pregunta, reemplazando el anterior si existía.</summary>
    Task GuardarProgresoAsync(ProgresoPregunta progreso, CancellationToken ct = default);
}
