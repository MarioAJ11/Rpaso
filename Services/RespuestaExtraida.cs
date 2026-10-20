using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rpaso.Services;

/// <summary>
/// Respuesta cruda que produce el modelo. Se mantiene separada de <see cref="Rpaso.Models.Pregunta"/>
/// porque el modelo no genera los identificadores de pregunta: los asigna la aplicación.
/// </summary>
public sealed record PreguntaExtraida(
    [property: JsonPropertyName("enunciado")] string Enunciado,
    [property: JsonPropertyName("opciones")] IReadOnlyList<OpcionExtraida> Opciones,
    [property: JsonPropertyName("claveRespuesta")] string ClaveRespuesta);

public sealed record OpcionExtraida(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("texto")] string Texto);

/// <summary>
/// Envoltura de la respuesta del modelo: <c>{"preguntas":[ ... ]}</c>.
/// </summary>
public sealed record RespuestaExtraida(
    [property: JsonPropertyName("preguntas")] IReadOnlyList<PreguntaExtraida> Preguntas);
