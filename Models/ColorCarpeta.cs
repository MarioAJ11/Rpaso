namespace Rpaso.Models;

/// <summary>
/// Un color de la paleta de carpetas. Los valores son cadenas hexadecimales para que
/// el modelo de dominio no dependa de Avalonia: la conversión a pincel ocurre en la capa de presentación.
/// </summary>
/// <param name="Id">Identificador estable del color.</param>
/// <param name="Nombre">Nombre para mostrar.</param>
/// <param name="Fondo">Color de fondo suave de la tarjeta.</param>
/// <param name="Borde">Color del borde.</param>
/// <param name="Texto">Color del icono y del texto.</param>
/// <param name="Icono">Color sólido del icono de carpeta.</param>
public sealed record ColorCarpeta(string Id, string Nombre, string Fondo, string Borde, string Texto, string Icono);

/// <summary>
/// Paleta fija de colores para las carpetas, con los mismos tonos que el diseño de referencia.
/// </summary>
public static class PaletaColores
{
    public static readonly IReadOnlyList<ColorCarpeta> Todos =
    [
        new("violet", "Violeta Repaso", "#F5F3FF", "#DDD9FA", "#5C54E5", "#5C54E5"),
        new("indigo", "Índigo", "#EEF2FF", "#C7D2FE", "#4F46E5", "#4F46E5"),
        new("emerald", "Esmeralda", "#ECFDF5", "#A7F3D0", "#059669", "#059669"),
        new("amber", "Ámbar", "#FFFBEB", "#FDE68A", "#D97706", "#D97706"),
        new("rose", "Rosa Coral", "#FFF1F2", "#FECDD3", "#E11D48", "#E11D48"),
        new("sky", "Cielo", "#F0F9FF", "#BAE6FD", "#0284C7", "#0284C7")
    ];

    /// <summary>Color por defecto cuando una carpeta no tiene un color válido.</summary>
    public static ColorCarpeta PorDefecto => Todos[0];

    public static ColorCarpeta Obtener(string? id)
        => Todos.FirstOrDefault(c => c.Id == id) ?? PorDefecto;
}
