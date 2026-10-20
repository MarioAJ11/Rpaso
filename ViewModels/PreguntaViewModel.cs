using Rpaso.Models;

namespace Rpaso.ViewModels;

/// <summary>
/// Presentación de una pregunta y su estado de repaso.
/// </summary>
public sealed class PreguntaViewModel : ViewModelBase
{
    public PreguntaViewModel(Pregunta pregunta, IReadOnlyDictionary<string, ProgresoPregunta> progreso)
    {
        Pregunta = pregunta;

        Fallada = progreso.TryGetValue(pregunta.Id, out var registro) && !registro.Acertada;
        Acertada = progreso.TryGetValue(pregunta.Id, out var registro2) && registro2.Acertada;
    }

    public Pregunta Pregunta { get; }

    public string Id => Pregunta.Id;

    public string Enunciado => Pregunta.Enunciado;

    public IReadOnlyList<Opcion> Opciones => Pregunta.Opciones;

    public string ClaveRespuesta => Pregunta.ClaveRespuesta;

    /// <summary>True si el usuario la falló en algún intento: entra en "por reforzar".</summary>
    public bool Fallada { get; }

    /// <summary>True si el usuario la acertó en el último intento.</summary>
    public bool Acertada { get; }
}
