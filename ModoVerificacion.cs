using System.Diagnostics;
using System.Text;
using Rpaso.Services;

namespace Rpaso;

/// <summary>
/// Modos de verificación por consola. Permiten probar el pipeline sin abrir la interfaz.
/// </summary>
internal static class ModoVerificacion
{
    /// <summary>
    /// Muestra el texto extraído del PDF, página a página. Sirve para comprobar si el PDF
    /// tiene capa de texto (si está escaneado, aquí saldría vacío).
    /// </summary>
    private static int MostrarTexto(string[] args)
    {
        if (args.Length == 0 || !File.Exists(args[0]))
        {
            Console.WriteLine("Uso: Rpaso.exe --texto <ruta.pdf>");
            return 1;
        }

        var paginas = new ServicioExtraccionPdf().ExtraerPaginasAsync(args[0]).GetAwaiter().GetResult();

        Console.WriteLine($"Páginas con texto: {paginas.Count}");

        for (var i = 0; i < paginas.Count; i++)
        {
            Console.WriteLine($"\n========== PÁGINA {i + 1} ({paginas[i].Length} caracteres) ==========");
            Console.WriteLine(paginas[i]);
        }

        return paginas.Count > 0 ? 0 : 2;
    }

    /// <summary>
    /// Ejecuta el pipeline completo (PDF -> texto -> modelo local -> preguntas) e imprime el resultado.
    /// </summary>
    private static int Ejecutar(string[] args)
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

    public static int Ejecutar(string modo, string[] args) => modo switch
    {
        "--texto" => MostrarTexto(args),
        _ => Ejecutar(args)
    };
}
