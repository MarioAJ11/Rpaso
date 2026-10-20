using Rpaso.Models;

namespace Rpaso.Services;

/// <summary>
/// Contrato de persistencia de la aplicación.
/// </summary>
public interface IServicioAlmacenamiento
{
    /// <summary>Carga todos los tests importados. Devuelve lista vacía si no hay datos.</summary>
    Task<IReadOnlyList<Test>> CargarTestsAsync(CancellationToken ct = default);

    /// <summary>Añade un test o lo reemplaza si ya existe uno con el mismo Id.</summary>
    Task GuardarTestAsync(Test test, CancellationToken ct = default);

    /// <summary>Carga el progreso del usuario sobre las preguntas ya respondidas.</summary>
    Task<IReadOnlyList<ProgresoPregunta>> CargarProgresoAsync(CancellationToken ct = default);

    /// <summary>Guarda el progreso de una pregunta, reemplazando el anterior si existía.</summary>
    Task GuardarProgresoAsync(ProgresoPregunta progreso, CancellationToken ct = default);
}
