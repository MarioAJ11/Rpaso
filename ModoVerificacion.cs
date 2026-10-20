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
    /// Vuelca la geometría de una página: palabras con sus coordenadas y altura de página.
    /// Sirve para calibrar los márgenes y ver qué hay en cada zona.
    /// </summary>
    private static int MostrarGeometria(string[] args)
    {
        if (args.Length == 0 || !File.Exists(args[0]))
        {
            Console.WriteLine("Uso: Rpaso.exe --geometria <ruta.pdf> [numPagina]");
            return 1;
        }

        var numeroPagina = args.Length > 1 && int.TryParse(args[1], out var n) ? n : 1;

        using var documento = UglyToad.PdfPig.PdfDocument.Open(args[0]);
        var pagina = documento.GetPages().Skip(numeroPagina - 1).FirstOrDefault();

        if (pagina is null)
        {
            Console.WriteLine($"El documento no tiene página {numeroPagina}.");
            return 1;
        }

        Console.WriteLine($"Página {numeroPagina}: {pagina.Width:F1} x {pagina.Height:F1} puntos");
        Console.WriteLine($"Palabras: {pagina.GetWords().Count()}");
        Console.WriteLine();
        Console.WriteLine("Y superior   Y inferior  X izq   X der   Texto");
        Console.WriteLine(new string('-', 100));

        foreach (var palabra in pagina.GetWords().OrderByDescending(p => p.BoundingBox.Bottom))
        {
            var caja = palabra.BoundingBox;
            Console.WriteLine($"{caja.Top,9:F1} {caja.Bottom,10:F1} {caja.Left,7:F1} {caja.Right,7:F1}  {palabra.Text}");
        }

        return 0;
    }

    /// <summary>
    /// Ejecuta el pipeline completo (PDF -> texto -> modelo local -> preguntas) e imprime el resultado.
    /// </summary>
    private static int Ejecutar(string[] args)
    {
        if (args.Length == 0 || !File.Exists(args[0]))
        {
            Console.WriteLine("Uso: Rpaso.exe --verificar <ruta.pdf>");
            Console.WriteLine($"Parser: geométrico (sin IA)");
            return 1;
        }

        var servicio = new ServicioImportacion(
            new ServicioParserExamen(),
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

    /// <summary>
    /// Muestra cómo el parser geométrico clasifica cada línea de una página.
    /// </summary>
    private static int MostrarAnalisis(string[] args)
    {
        if (args.Length == 0 || !File.Exists(args[0]))
        {
            Console.WriteLine("Uso: Rpaso.exe --analizar <ruta.pdf> [numPagina]");
            return 1;
        }

        var numeroPagina = args.Length > 1 && int.TryParse(args[1], out var n) ? n : 1;
        var parser = new ServicioParserExamen();

        Console.WriteLine($"Página {numeroPagina}");
        Console.WriteLine("Tipo        Y base    X izq   Texto");
        Console.WriteLine(new string('-', 110));

        foreach (var (baseLinea, izquierda, texto, tipo, letra) in parser.DiagnosticarPagina(args[0], numeroPagina))
        {
            var marca = letra is null ? "" : $" [{letra}]";
            Console.WriteLine($"{tipo,-12} {baseLinea,7:F1} {izquierda,7:F1}  {texto}{marca}");
        }

        return 0;
    }

    public static int Ejecutar(string modo, string[] args) => modo switch
    {
        "--texto" => MostrarTexto(args),
        "--geometria" => MostrarGeometria(args),
        "--analizar" => MostrarAnalisis(args),
        _ => Ejecutar(args)
    };
}
