using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rpaso.Services;

namespace Rpaso.ViewModels;

/// <summary>Una fila del informe de importación.</summary>
public sealed class ResultadoImportacionViewModel : ViewModelBase
{
    public ResultadoImportacionViewModel(ResultadoImportacion resultado)
    {
        NombreArchivo = resultado.NombreArchivo;
        Exito = resultado.Exito;
        TotalPreguntas = resultado.TotalPreguntas;
        TotalDescartadas = resultado.TotalDescartadas;
        MensajeError = resultado.MensajeError;
    }

    public string NombreArchivo { get; }

    public bool Exito { get; }

    public int TotalPreguntas { get; }

    public int TotalDescartadas { get; }

    public string? MensajeError { get; }

    public string Detalle => Exito
        ? $"{TotalPreguntas} {(TotalPreguntas == 1 ? "pregunta" : "preguntas")}"
            + (TotalDescartadas > 0
                ? $" · {TotalDescartadas} {(TotalDescartadas == 1 ? "descartada" : "descartadas")}"
                : string.Empty)
        : MensajeError ?? "Error desconocido";
}

/// <summary>
/// Informe de una importación de varios archivos: qué se importó y qué falló.
/// </summary>
public sealed class InformeImportacionViewModel : ViewModelBase
{
    public InformeImportacionViewModel(InformeImportacion informe, Action cerrar)
    {
        Importados = informe.Importados.Select(r => new ResultadoImportacionViewModel(r)).ToList();
        Fallidos = informe.Fallidos.Select(r => new ResultadoImportacionViewModel(r)).ToList();
        TotalImportados = informe.TotalImportados;
        TotalFallidos = informe.TotalFallidos;
        TotalPreguntas = informe.TotalPreguntas;
        TotalDescartadas = informe.TotalDescartadas;

        CerrarCommand = new RelayCommand(cerrar);
    }

    public IReadOnlyList<ResultadoImportacionViewModel> Importados { get; }

    public IReadOnlyList<ResultadoImportacionViewModel> Fallidos { get; }

    public int TotalImportados { get; }

    public int TotalFallidos { get; }

    public int TotalPreguntas { get; }

    public int TotalDescartadas { get; }

    public bool HayFallidos => TotalFallidos > 0;

    public bool HayImportados => TotalImportados > 0;

    public string Titulo => TotalFallidos > 0
        ? "Importación completada con errores"
        : "Importación completada";

    public string Resumen => $"{(TotalImportados == 1 ? "1 examen importado" : $"{TotalImportados} exámenes importados")}"
        + (TotalFallidos > 0 ? $" · {TotalFallidos} fallidos" : string.Empty)
        + (TotalPreguntas > 0 ? $" · {TotalPreguntas} preguntas" : string.Empty)
        + (TotalDescartadas > 0 ? $" · {TotalDescartadas} descartadas" : string.Empty);

    public RelayCommand CerrarCommand { get; }
}
