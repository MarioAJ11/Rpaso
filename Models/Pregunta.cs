using System.Text.Json.Serialization;

namespace Rpaso.Models;

/// <summary>
/// Pregunta de tipo test con sus opciones y la clave de respuesta correcta.
/// </summary>
/// <param name="Id">Identificador único de la pregunta dentro del test.</param>
/// <param name="Enunciado">Texto de la pregunta.</param>
/// <param name="Opciones">Opciones disponibles. Nunca vacío.</param>
/// <param name="ClaveRespuesta">
/// Id de la opción correcta ("a", "b"...). Se referencia por Id y no por texto
/// para que acentos o saltos de línea no rompan la comparación.
/// </param>
public record Pregunta(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("enunciado")] string Enunciado,
    [property: JsonPropertyName("opciones")] IReadOnlyList<Opcion> Opciones,
    [property: JsonPropertyName("claveRespuesta")] string ClaveRespuesta);
