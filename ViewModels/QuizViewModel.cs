using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Rpaso.ViewModels;

/// <summary>
/// Sesión de práctica (modo examen).
/// </summary>
public sealed partial class QuizViewModel : ViewModelBase
{
    private readonly Func<string, bool, Task> _registrarResultado;
    private readonly Func<Task>? _alSalir;
    private readonly Dictionary<string, string> _respuestas = new(StringComparer.Ordinal);

    public QuizViewModel(
        string titulo,
        IReadOnlyList<PreguntaViewModel> preguntas,
        Func<string, bool, Task> registrarResultado,
        Func<Task>? alSalir = null)
    {
        Titulo = titulo;
        Preguntas = preguntas;
        _registrarResultado = registrarResultado;
        _alSalir = alSalir;

        CargarOpciones();
    }

    public string Titulo { get; }

    public IReadOnlyList<PreguntaViewModel> Preguntas { get; }

    /// <summary>Opciones de la pregunta actual, con su estado visual.</summary>
    public ObservableCollection<OpcionViewModel> Opciones { get; } = [];

    public ObservableCollection<QuizPreguntaResultadoViewModel> Resultados { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreguntaActual))]
    [NotifyPropertyChangedFor(nameof(NumeroPregunta))]
    [NotifyPropertyChangedFor(nameof(EsUltima))]
    [NotifyPropertyChangedFor(nameof(EsPrimera))]
    [NotifyPropertyChangedFor(nameof(PuedeIrAnterior))]
    [NotifyPropertyChangedFor(nameof(PuedeIrSiguiente))]
    private int _indiceActual;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EsPracticando))]
    private bool _esFinalizado;

    [ObservableProperty]
    private string? _respuestaSeleccionada;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RespuestaEsCorrecta))]
    [NotifyPropertyChangedFor(nameof(RespuestaEsIncorrecta))]
    private bool _corregido;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PuedeIrAnterior))]
    [NotifyPropertyChangedFor(nameof(PuedeIrSiguiente))]
    [NotifyPropertyChangedFor(nameof(PuedeSalir))]
    private bool _guardandoResultado;

    [ObservableProperty]
    private int _aciertos;

    [ObservableProperty]
    private int _respondidas;

    public PreguntaViewModel PreguntaActual => Preguntas[IndiceActual];

    /// <summary>Número de pregunta visible, empezando en 1.</summary>
    public int NumeroPregunta => IndiceActual + 1;

    public bool EsUltima => IndiceActual == Preguntas.Count - 1;

    public bool EsPrimera => IndiceActual == 0;

    public bool EsPracticando => !EsFinalizado;

    public bool PuedeIrAnterior => !GuardandoResultado && !EsPrimera;

    public bool PuedeIrSiguiente => !GuardandoResultado && !EsUltima;

    public bool PuedeSalir => !GuardandoResultado;

    public bool RespuestaEsCorrecta => Corregido && PreguntaActual.ClaveRespuesta == RespuestaSeleccionada;

    public bool RespuestaEsIncorrecta => Corregido && !RespuestaEsCorrecta;

    public string ResultadoTexto => Respondidas == 0 && !EsFinalizado
        ? string.Empty
        : $"{Aciertos} correctas · {Respondidas - Aciertos} incorrectas · {Preguntas.Count - Respondidas} sin responder";

    partial void OnIndiceActualChanged(int value) => CargarOpciones();

    partial void OnRespuestaSeleccionadaChanged(string? value)
    {
        foreach (var opcion in Opciones)
            opcion.Seleccionada = opcion.Id == value;
    }

    partial void OnCorregidoChanged(bool value)
    {
        foreach (var opcion in Opciones)
            opcion.Corregido = value;
    }

    private void CargarOpciones()
    {
        Opciones.Clear();

        var pregunta = PreguntaActual;
        var clave = pregunta.ClaveRespuesta;

        foreach (var opcion in pregunta.Opciones.Select(o => new OpcionViewModel(o, clave)))
            Opciones.Add(opcion);

        Corregido = false;
        if (_respuestas.TryGetValue(pregunta.Id, out var respuesta))
        {
            RespuestaSeleccionada = respuesta;
            Corregido = true;
        }
        else
        {
            RespuestaSeleccionada = null;
        }
    }

    [RelayCommand]
    private async Task SeleccionarAsync(string id)
    {
        if (!Corregido && !GuardandoResultado)
        {
            RespuestaSeleccionada = id;
            GuardandoResultado = true;
            try
            {
                var acertada = PreguntaActual.ClaveRespuesta == id;
                await _registrarResultado(PreguntaActual.Id, acertada);

                _respuestas[PreguntaActual.Id] = id;
                Corregido = true;
                Respondidas++;
                if (acertada)
                    Aciertos++;

                OnPropertyChanged(nameof(ResultadoTexto));
            }
            finally
            {
                GuardandoResultado = false;
            }
        }
    }

    [RelayCommand]
    private void Anterior()
    {
        if (!PuedeIrAnterior)
            return;

        IndiceActual--;
    }

    [RelayCommand]
    private void Siguiente()
    {
        if (!PuedeIrSiguiente)
            return;

        IndiceActual++;
    }

    [RelayCommand]
    private void VerResultados()
    {
        if (!EsUltima || GuardandoResultado)
            return;

        Resultados.Clear();
        for (var i = 0; i < Preguntas.Count; i++)
        {
            var pregunta = Preguntas[i];
            _respuestas.TryGetValue(pregunta.Id, out var respuesta);
            Resultados.Add(new QuizPreguntaResultadoViewModel(i + 1, pregunta, respuesta));
        }

        EsFinalizado = true;
        OnPropertyChanged(nameof(ResultadoTexto));
    }

    [RelayCommand]
    private async Task SalirAsync()
    {
        if (PuedeSalir && _alSalir is not null)
            await _alSalir();
    }
}
