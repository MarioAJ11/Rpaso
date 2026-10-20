using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rpaso.Models;

namespace Rpaso.ViewModels;

/// <summary>Opción en edición: su texto y si es la correcta.</summary>
public sealed partial class OpcionEditorViewModel : ViewModelBase
{
    private readonly Action<OpcionEditorViewModel> _alMarcarComoCorrecta;
    private bool _esCorrecta;

    public OpcionEditorViewModel(string letra, string texto, bool esCorrecta, Action<OpcionEditorViewModel> alMarcarComoCorrecta)
    {
        Letra = letra;
        Texto = texto;
        _esCorrecta = esCorrecta;
        _alMarcarComoCorrecta = alMarcarComoCorrecta;
    }

    public string Letra { get; internal set; }

    public string Texto { get; set; }

    public bool EsCorrecta
    {
        get => _esCorrecta;
        set
        {
            if (SetProperty(ref _esCorrecta, value) && value)
                _alMarcarComoCorrecta(this);
        }
    }
}

/// <summary>Pregunta en edición: enunciado, opciones y respuesta correcta.</summary>
public sealed partial class PreguntaEditorViewModel : ViewModelBase
{
    private string _enunciado;

    public PreguntaEditorViewModel(Pregunta pregunta, string id)
    {
        Id = id;
        _enunciado = pregunta.Enunciado;

        foreach (var opcion in pregunta.Opciones)
            Opciones.Add(new OpcionEditorViewModel(opcion.Id, opcion.Texto, opcion.Id == pregunta.ClaveRespuesta, MarcarComoCorrecta));

        Renumerar();
    }

    public string Id { get; }

    public string Enunciado
    {
        get => _enunciado;
        set => SetProperty(ref _enunciado, value);
    }

    public ObservableCollection<OpcionEditorViewModel> Opciones { get; } = [];

    public void MarcarComoCorrecta(OpcionEditorViewModel opcion)
    {
        foreach (var otra in Opciones)
        {
            if (!ReferenceEquals(otra, opcion))
                otra.EsCorrecta = false;
        }
    }

    [RelayCommand]
    private void MarcarOpcionComoCorrecta(OpcionEditorViewModel? opcion)
    {
        if (opcion is null)
            return;

        opcion.EsCorrecta = true;
    }

    [RelayCommand]
    private void AñadirOpcion()
    {
        Opciones.Add(new OpcionEditorViewModel("", "", false, MarcarComoCorrecta));
        Renumerar();
    }

    [RelayCommand]
    private void EliminarOpcion(OpcionEditorViewModel? opcion)
    {
        if (opcion is null)
            return;

        Opciones.Remove(opcion);
        Renumerar();
    }

    /// <summary>Renumera las opciones (a, b, c…) y mantiene válida la respuesta correcta.</summary>
    private void Renumerar()
    {
        var letras = "abcdefghij".ToCharArray();

        for (var i = 0; i < Opciones.Count; i++)
        {
            var letra = i < letras.Length ? letras[i].ToString() : ((char)('a' + i)).ToString();
            Opciones[i].Letra = letra;
        }
    }
}

/// <summary>Editor de un test completo: enunciados, opciones y la respuesta correcta.</summary>
public sealed partial class EditorTestViewModel : ViewModelBase
{
    private readonly Test _original;
    private readonly Func<Test, Task> _guardar;
    private readonly Action _cancelar;
    private string _titulo;

    public EditorTestViewModel(Test test, Func<Test, Task> guardar, Action cancelar)
    {
        _original = test;
        _guardar = guardar;
        _cancelar = cancelar;
        _titulo = test.Titulo;

        for (var i = 0; i < test.Preguntas.Count; i++)
            Preguntas.Add(new PreguntaEditorViewModel(test.Preguntas[i], $"p-{i}"));
    }

    public string Id => _original.Id;

    public string CarpetaId => _original.CarpetaId;

    public string Titulo
    {
        get => _titulo;
        set => SetProperty(ref _titulo, value);
    }

    public ObservableCollection<PreguntaEditorViewModel> Preguntas { get; } = [];

    [RelayCommand]
    private void AñadirPregunta()
    {
        var vacia = new Pregunta("", "", [], "a");
        Preguntas.Add(new PreguntaEditorViewModel(vacia, $"p-{Preguntas.Count}"));
    }

    [RelayCommand]
    private void EliminarPregunta(PreguntaEditorViewModel? pregunta)
    {
        if (pregunta is null)
            return;

        Preguntas.Remove(pregunta);
    }

    [RelayCommand]
    private async Task GuardarAsync()
    {
        var preguntas = Preguntas
            .Where(p => p.Enunciado.Trim().Length > 0 && p.Opciones.Count >= 2)
            .Select((p, i) => new Pregunta(
                $"q-{i + 1}",
                p.Enunciado.Trim(),
                p.Opciones
                    .Where(o => o.Texto.Trim().Length > 0)
                    .Select(o => new Opcion(o.Letra, o.Texto.Trim()))
                    .ToList(),
                p.Opciones.FirstOrDefault(o => o.EsCorrecta)?.Letra ?? "a"))
            .ToList();

        var test = _original with
        {
            Titulo = Titulo.Trim().Length == 0 ? _original.Titulo : Titulo.Trim(),
            Preguntas = preguntas
        };

        await _guardar(test);
    }

    [RelayCommand]
    private void Cancelar() => _cancelar();
}
