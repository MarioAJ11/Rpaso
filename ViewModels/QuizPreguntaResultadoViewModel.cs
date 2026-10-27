namespace Rpaso.ViewModels;

public sealed class QuizPreguntaResultadoViewModel
{
    public QuizPreguntaResultadoViewModel(int numero, PreguntaViewModel pregunta, string? respuestaSeleccionada)
    {
        Numero = numero;
        Enunciado = pregunta.Enunciado;
        EsCorrecta = respuestaSeleccionada == pregunta.ClaveRespuesta;
        SinResponder = respuestaSeleccionada is null;
        Estado = SinResponder ? "Sin responder" : EsCorrecta ? "Correcta" : "Incorrecta";
        Opciones = pregunta.Opciones
            .Select(opcion => new QuizOpcionResultadoViewModel(
                opcion.Id,
                opcion.Texto,
                opcion.Id == pregunta.ClaveRespuesta,
                opcion.Id == respuestaSeleccionada))
            .ToList();
    }

    public int Numero { get; }

    public string Enunciado { get; }

    public string Estado { get; }

    public bool EsCorrecta { get; }

    public bool EsIncorrecta => !SinResponder && !EsCorrecta;

    public bool SinResponder { get; }

    public IReadOnlyList<QuizOpcionResultadoViewModel> Opciones { get; }
}

public sealed class QuizOpcionResultadoViewModel
{
    public QuizOpcionResultadoViewModel(string id, string texto, bool esCorrecta, bool fueSeleccionada)
    {
        Id = id;
        Texto = texto;
        EsCorrecta = esCorrecta;
        FueSeleccionada = fueSeleccionada;
    }

    public string Id { get; }

    public string Texto { get; }

    public bool EsCorrecta { get; }

    public bool FueSeleccionada { get; }

    public bool MostrarError => FueSeleccionada && !EsCorrecta;
}
