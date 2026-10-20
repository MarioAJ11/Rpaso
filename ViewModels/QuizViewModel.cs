using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Rpaso.ViewModels;

/// <summary>
/// Sesión de práctica (modo examen).
/// </summary>
public sealed partial class QuizViewModel : ViewModelBase
{
    private readonly Action<string, bool> _registrarResultado;
    private readonly Action? _alSalir;

    public QuizViewModel(
        string titulo,
        IReadOnlyList<PreguntaViewModel> preguntas,
        Action<string, bool> registrarResultado,
        Action? alSalir = null)
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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreguntaActual))]
    [NotifyPropertyChangedFor(nameof(NumeroPregunta))]
    [NotifyPropertyChangedFor(nameof(EsUltima))]
    [NotifyPropertyChangedFor(nameof(EsPrimera))]
    private int _indiceActual;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PuedeComprobar))]
    private string? _respuestaSeleccionada;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PuedeComprobar))]
    [NotifyPropertyChangedFor(nameof(RespuestaEsCorrecta))]
    [NotifyPropertyChangedFor(nameof(RespuestaEsIncorrecta))]
    private bool _corregido;

    [ObservableProperty]
    private int _aciertos;

    [ObservableProperty]
    private int _respondidas;

    public PreguntaViewModel PreguntaActual => Preguntas[IndiceActual];

    /// <summary>Número de pregunta visible, empezando en 1.</summary>
    public int NumeroPregunta => IndiceActual + 1;

    public bool EsUltima => IndiceActual == Preguntas.Count - 1;

    public bool EsPrimera => IndiceActual == 0;

    public bool PuedeComprobar => !Corregido && RespuestaSeleccionada is not null;

    public bool RespuestaEsCorrecta => Corregido && PreguntaActual.ClaveRespuesta == RespuestaSeleccionada;

    public bool RespuestaEsIncorrecta => Corregido && !RespuestaEsCorrecta;

    public string ResultadoTexto => Respondidas == 0
        ? string.Empty
        : $"{Aciertos} de {Preguntas.Count} correctas";

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

        var clave = PreguntaActual.ClaveRespuesta;

        foreach (var opcion in PreguntaActual.Opciones)
            Opciones.Add(new OpcionViewModel(opcion, clave));

        RespuestaSeleccionada = null;
        Corregido = false;
    }

    [RelayCommand]
    private void Seleccionar(string id)
    {
        if (!Corregido)
            RespuestaSeleccionada = id;
    }

    [RelayCommand]
    private void Anterior()
    {
        if (EsPrimera)
            return;

        IndiceActual--;
    }

    [RelayCommand]
    private void Siguiente()
    {
        if (EsUltima)
            return;

        IndiceActual++;
    }

    [RelayCommand]
    private void Comprobar()
    {
        if (!PuedeComprobar)
            return;

        Corregido = true;

        var acertada = PreguntaActual.ClaveRespuesta == RespuestaSeleccionada;
        Respondidas++;
        if (acertada)
            Aciertos++;

        _registrarResultado(PreguntaActual.Id, acertada);
        OnPropertyChanged(nameof(ResultadoTexto));
    }

    [RelayCommand]
    private void Salir() => _alSalir?.Invoke();
}
