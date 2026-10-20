using CommunityToolkit.Mvvm.ComponentModel;
using Rpaso.Models;

namespace Rpaso.ViewModels;

/// <summary>
/// Una opción dentro del quiz, con su estado visual: seleccionada, correcta o fallada.
/// </summary>
public sealed partial class OpcionViewModel : ViewModelBase
{
    private readonly string _claveRespuesta;

    public OpcionViewModel(Opcion opcion, string claveRespuesta)
    {
        Opcion = opcion;
        _claveRespuesta = claveRespuesta;
    }

    public Opcion Opcion { get; }

    public string Id => Opcion.Id;

    public string Texto => Opcion.Texto;

    /// <summary>True si esta opción es la clave de respuesta del examen.</summary>
    public bool EsCorrecta => string.Equals(Id, _claveRespuesta, StringComparison.OrdinalIgnoreCase);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostrarSeleccionada))]
    [NotifyPropertyChangedFor(nameof(MostrarFallo))]
    private bool _seleccionada;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostrarAcierto))]
    [NotifyPropertyChangedFor(nameof(MostrarFallo))]
    [NotifyPropertyChangedFor(nameof(MostrarSeleccionada))]
    private bool _corregido;

    /// <summary>Resaltado violeta: el usuario la ha elegido y aún no se ha corregido.</summary>
    public bool MostrarSeleccionada => Seleccionada && !Corregido;

    /// <summary>Check verde: se muestra solo tras corregir y solo en la opción correcta.</summary>
    public bool MostrarAcierto => Corregido && EsCorrecta;

    /// <summary>Cruz roja: la eligió el usuario y no era la correcta.</summary>
    public bool MostrarFallo => Corregido && Seleccionada && !EsCorrecta;
}
