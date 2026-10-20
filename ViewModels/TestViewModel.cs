using System.Globalization;
using Rpaso.Models;

namespace Rpaso.ViewModels;

/// <summary>
/// Presentación de un test importado desde un PDF.
/// </summary>
public sealed class TestViewModel : ViewModelBase
{
    public TestViewModel(Test test, IReadOnlyDictionary<string, ProgresoPregunta> progreso)
    {
        Test = test;
        Preguntas = test.Preguntas.Select(p => new PreguntaViewModel(p, progreso)).ToList();
    }

    public Test Test { get; }

    public string Id => Test.Id;

    public string Titulo => Test.Titulo;

    /// <summary>Fecha corta tipo "19 oct".</summary>
    public string Fecha => Test.FechaImportacion.ToString("d MMM", CultureInfo.CurrentCulture);

    public IReadOnlyList<PreguntaViewModel> Preguntas { get; }

    public int TotalPreguntas => Preguntas.Count;

    public int TotalFalladas => Preguntas.Count(p => p.Fallada);

    public bool HayFalladas => TotalFalladas > 0;

    /// <summary>Preguntas ya falladas: son las que se repasan.</summary>
    public IReadOnlyList<PreguntaViewModel> PreguntasFalladas => Preguntas.Where(p => p.Fallada).ToList();
}
