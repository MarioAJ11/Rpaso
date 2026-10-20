using System.Text.Json.Serialization;

namespace Rpaso.Models;

/// <summary>
/// Progreso del usuario sobre una pregunta concreta.
/// </summary>
/// <param name="PreguntaId">Id de la pregunta sobre la que se registra el progreso.</param>
/// <param name="Acertada">Resultado del último intento: true si se acertó, false si se falló.</param>
/// <param name="NumeroFallos">Cuántas veces se ha fallado.</param>
/// <param name="UltimoIntento">Momento del último intento.</param>
public record ProgresoPregunta(
    [property: JsonPropertyName("preguntaId")] string PreguntaId,
    [property: JsonPropertyName("acertada")] bool Acertada,
    [property: JsonPropertyName("numeroFallos")] int NumeroFallos,
    [property: JsonPropertyName("ultimoIntento")] DateTimeOffset UltimoIntento);
