using System.Text.Json.Serialization;

namespace Rpaso.Models;

/// <summary>
/// Un test (examen) importado desde un PDF, con su carpeta temática y sus preguntas.
/// </summary>
/// <param name="Id">Identificador único del test.</param>
/// <param name="Titulo">Título del test (normalmente el nombre del PDF de origen).</param>
/// <param name="Carpeta">Carpeta temática a la que pertenece. Determina el agrupamiento en la UI.</param>
/// <param name="Preguntas">Preguntas del test. Puede estar vacío si el parseo no encontró ninguna.</param>
public record Test(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("titulo")] string Titulo,
    [property: JsonPropertyName("carpeta")] string Carpeta,
    [property: JsonPropertyName("preguntas")] IReadOnlyList<Pregunta> Preguntas);
