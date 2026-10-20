using Rpaso.Models;

namespace Rpaso.Services;

/// <summary>
/// Resultado de intentar importar un PDF concreto.
/// </summary>
/// <param name="RutaArchivo">Ruta completa del PDF.</param>
/// <param name="NombreArchivo">Nombre del archivo, para mostrarlo.</param>
/// <param name="Exito">True si se ha importado con preguntas.</param>
/// <param name="TotalPreguntas">Número de preguntas extraídas (0 si falló).</param>
/// <param name="MensajeError">Motivo del fallo, o null si salió bien.</param>
public sealed record ResultadoImportacion(
    string RutaArchivo,
    string NombreArchivo,
    bool Exito,
    int TotalPreguntas,
    string? MensajeError)
{
    public static ResultadoImportacion Importado(string ruta, int preguntas)
        => new(ruta, Path.GetFileName(ruta), true, preguntas, null);

    public static ResultadoImportacion Fallido(string ruta, string motivo)
        => new(ruta, Path.GetFileName(ruta), false, 0, motivo);
}

/// <summary>
/// Resumen de una importación de varios archivos.
/// </summary>
public sealed class InformeImportacion
{
    public InformeImportacion(IReadOnlyList<ResultadoImportacion> resultados)
    {
        Resultados = resultados;
    }

    public IReadOnlyList<ResultadoImportacion> Resultados { get; }

    public IReadOnlyList<ResultadoImportacion> Importados => Resultados.Where(r => r.Exito).ToList();

    public IReadOnlyList<ResultadoImportacion> Fallidos => Resultados.Where(r => !r.Exito).ToList();

    public int TotalImportados => Importados.Count;

    public int TotalFallidos => Fallidos.Count;

    public int TotalPreguntas => Importados.Sum(r => r.TotalPreguntas);

    public bool HayFallos => TotalFallidos > 0;
}
