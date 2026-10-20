using System.Text.Json.Serialization;

namespace Rpaso.Models;

/// <summary>
/// Una opción de respuesta de una pregunta.
/// </summary>
/// <param name="Id">Identificador estable de la opción ("a", "b", "c"...).</param>
/// <param name="Texto">Texto visible de la opción.</param>
public record Opcion(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("texto")] string Texto);
