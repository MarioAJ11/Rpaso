using System.Diagnostics;
using Rpaso.Services;

namespace Rpaso;

/// <summary>
/// Modo de verificación por consola. Permite probar el pipeline completo
/// (PDF -> texto -> modelo local -> preguntas) sin abrir la interfaz gráfica.
/// </summary>
internal static class ModoVerificacion
{
    public static int Ejecutar(string[] args)
    {
        if (args.Length == 0 || !File.Exists(args[0]))
        {
            Console.WriteLine("Uso: Rpaso.exe --verificar <ruta.pdf>");
            Console.WriteLine($"Modelo detectado: {ServicioEstructuracionIa.BuscarModelo() ?? "(ninguno)"}");
            return 1;
        }

        var servicio = new ServicioImportacion(
            new ServicioExtraccionPdf(),
            new ServicioEstructuracionIa(),
            new ServicioAlmacenamiento());

        var cronometro = Stopwatch.StartNew();
        var test = servicio.ImportarAsync(args[0], "").GetAwaiter().GetResult();

        Console.WriteLine();
        Console.WriteLine($"Test      : {test.Titulo}");
        Console.WriteLine($"Origen    : {test.ArchivoOrigen}");
        Console.WriteLine($"Preguntas : {test.Preguntas.Count}");
        Console.WriteLine($"Tiempo    : {cronometro.ElapsedMilliseconds} ms");
        Console.WriteLine();

        foreach (var pregunta in test.Preguntas)
        {
            Console.WriteLine($"--- {pregunta.Id} ---");
            Console.WriteLine($"  {pregunta.Enunciado}");
            foreach (var opcion in pregunta.Opciones)
            {
                var marca = opcion.Id == pregunta.ClaveRespuesta ? "  <== correcta" : "";
                Console.WriteLine($"    {opcion.Id}) {opcion.Texto}{marca}");
            }
            Console.WriteLine();
        }

        return test.Preguntas.Count > 0 ? 0 : 2;
    }
}
