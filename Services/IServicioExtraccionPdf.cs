namespace Rpaso.Services;

/// <summary>
/// Extrae el texto crudo de un PDF.
/// </summary>
public interface IServicioExtraccionPdf
{
    /// <summary>
    /// Devuelve el texto de cada página, en orden. Se mantiene la separación en páginas porque
    /// permite trocear el documento antes de enviarlo al modelo sin partir preguntas por la mitad.
    /// </summary>
    Task<IReadOnlyList<string>> ExtraerPaginasAsync(string rutaPdf, CancellationToken ct = default);
}
