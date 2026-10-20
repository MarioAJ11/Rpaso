using System.Text.Json.Serialization;

namespace Rpaso.Models;

/// <summary>
/// Un test (examen) importado desde un PDF, con su carpeta temática y sus preguntas.
/// </summary>
/// <param name="Id">Identificador único del test.</param>
/// <param name="Titulo">Título del test.</param>
/// <param name="CarpetaId">Id de la <see cref="Carpeta"/> a la que pertenece.</param>
/// <param name="FechaImportacion">Momento de la importación.</param>
/// <param name="ArchivoOrigen">Nombre del PDF de origen, para poder reubicarlo o reimportarlo.</param>
/// <param name="Preguntas">Preguntas del test. Puede estar vacío si el parseo no encontró ninguna.</param>
public record Test(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("titulo")] string Titulo,
    [property: JsonPropertyName("carpetaId")] string CarpetaId,
    [property: JsonPropertyName("fechaImportacion")] DateTimeOffset FechaImportacion,
    [property: JsonPropertyName("archivoOrigen")] string ArchivoOrigen,
    [property: JsonPropertyName("preguntas")] IReadOnlyList<Pregunta> Preguntas);
