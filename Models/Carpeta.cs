using System.Text.Json.Serialization;

namespace Rpaso.Models;

/// <summary>
/// Una carpeta temática que agrupa tests.
/// </summary>
/// <param name="Id">Identificador único.</param>
/// <param name="Nombre">Nombre visible de la carpeta (asignatura o tema).</param>
/// <param name="Descripcion">Descripción corta opcional.</param>
/// <param name="ColorId">Identificador del color de la paleta. Ver <see cref="ColorCarpeta"/>.</param>
public record Carpeta(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("nombre")] string Nombre,
    [property: JsonPropertyName("descripcion")] string Descripcion,
    [property: JsonPropertyName("colorId")] string ColorId);
