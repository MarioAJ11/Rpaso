using System.Diagnostics;
using System.Text;
using Rpaso.Models;
using Rpaso.Services;
using UglyToad.PdfPig.Content;

namespace Rpaso;

/// <summary>
/// Modos de verificaciÃ³n por consola. Permiten probar el pipeline sin abrir la interfaz.
/// </summary>
internal static class ModoVerificacion
{
    /// <summary>
    /// Muestra el texto extraÃ­do del PDF, pÃ¡gina a pÃ¡gina. Sirve para comprobar si el PDF
    /// tiene capa de texto (si estÃ¡ escaneado, aquÃ­ saldrÃ­a vacÃ­o).
    /// </summary>
    private static int MostrarTexto(string[] args)
    {
        if (args.Length == 0 || !File.Exists(args[0]))
        {
            Console.WriteLine("Uso: Rpaso.exe --texto <ruta.pdf>");
            return 1;
        }

        var paginas = new ServicioExtraccionPdf().ExtraerPaginasAsync(args[0]).GetAwaiter().GetResult();

        Console.WriteLine($"PÃ¡ginas con texto: {paginas.Count}");

        for (var i = 0; i < paginas.Count; i++)
        {
            Console.WriteLine($"\n========== PÃGINA {i + 1} ({paginas[i].Length} caracteres) ==========");
            Console.WriteLine(paginas[i]);
        }

        return paginas.Count > 0 ? 0 : 2;
    }

    /// <summary>
    /// Vuelca la geometrÃ­a de una pÃ¡gina: palabras con sus coordenadas y altura de pÃ¡gina.
    /// Sirve para calibrar los mÃ¡rgenes y ver quÃ© hay en cada zona.
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
            Console.WriteLine($"El documento no tiene pÃ¡gina {numeroPagina}.");
            return 1;
        }

        Console.WriteLine($"PÃ¡gina {numeroPagina}: {pagina.Width:F1} x {pagina.Height:F1} puntos");
        Console.WriteLine($"Palabras: {pagina.GetWords().Count()}");
        Console.WriteLine();
        Console.WriteLine("Y superior   Y inferior  X izq   X der   Fuente                 Texto");
        Console.WriteLine(new string('-', 118));

        foreach (var palabra in pagina.GetWords().OrderByDescending(p => p.BoundingBox.Bottom))
        {
            var caja = palabra.BoundingBox;
            var fuente = palabra.Letters.Count > 0 ? palabra.Letters[0].FontName : "?";
            Console.WriteLine($"{caja.Top,9:F1} {caja.Bottom,10:F1} {caja.Left,7:F1} {caja.Right,7:F1}  {fuente,-22} {palabra.Text}");
        }

        // Trazos vectoriales: asÃ­ se dibujan las casillas marcadas de un test.
        try
        {
            var trazos = pagina.Paths;
            Console.WriteLine();
            Console.WriteLine($"Trazos vectoriales en la pagina: {trazos.Count}");

            foreach (var trazo in trazos.Take(20))
            {
                var caja = trazo.GetBoundingRectangle();

                Console.WriteLine($"  bbox={caja}  relleno={trazo.IsFilled} puntos={trazo.Count} color={trazo.FillColor}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"No se pudieron leer los trazos: {ex.Message}");
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
            Console.WriteLine($"Parser: geomÃ©trico (sin IA)");
            return 1;
        }

        var servicio = new ServicioImportacion(
            new ServicioParserExamen(new ServicioOcrWindows()),
            new ServicioAlmacenamiento());

        var cronometro = Stopwatch.StartNew();

        Test test;
        try
        {
            test = servicio.ImportarAsync(args[0], "").GetAwaiter().GetResult();
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine();
            Console.WriteLine($"No se pudo extraer nada: {ex.Message}");
            return 2;
        }

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
    /// Muestra cÃ³mo el parser geomÃ©trico clasifica cada lÃ­nea de una pÃ¡gina.
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

        Console.WriteLine($"PÃ¡gina {numeroPagina}");
        Console.WriteLine("Tipo        Y base    X izq   X cont  Negrita  Texto");
        Console.WriteLine(new string('-', 122));

        foreach (var (baseLinea, izquierda, xContenido, texto, tipo, letra, negrita) in parser.DiagnosticarPagina(args[0], numeroPagina))
        {
            var marca = letra is null ? "" : $" [{letra}]";
            Console.WriteLine($"{tipo,-12} {baseLinea,7:F1} {izquierda,7:F1} {xContenido,7:F1}  {(negrita ? "SI" : "-"),-9} {texto}{marca}");
        }

        return 0;
    }

    /// <summary>
    /// Importa varios PDFs y muestra el informe: quÃ© se importÃ³ y por quÃ© fallÃ³ lo que fallÃ³.
    /// </summary>
    private static int ImportarVarios(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Uso: Rpaso.exe --importar <ruta1.pdf> [ruta2.pdf ...]");
            return 1;
        }

        var servicio = new ServicioImportacion(
            new ServicioParserExamen(new ServicioOcrWindows()),
            new ServicioAlmacenamiento());

        var cronometro = Stopwatch.StartNew();
        var informe = servicio.ImportarVariosAsync(args, "").GetAwaiter().GetResult();

        Console.WriteLine();
        Console.WriteLine("â•â•â•â•â•â• IMPORTACIÃ“N â•â•â•â•â•â•");
        Console.WriteLine($"Importados : {informe.TotalImportados} ({informe.TotalPreguntas} preguntas)");
        Console.WriteLine($"Fallidos   : {informe.TotalFallidos}");
        Console.WriteLine($"Tiempo     : {cronometro.ElapsedMilliseconds} ms");
        Console.WriteLine();

        foreach (var resultado in informe.Importados)
            Console.WriteLine($"  [OK]   {resultado.NombreArchivo,-55} {resultado.TotalPreguntas,4} preguntas");

        foreach (var resultado in informe.Fallidos)
            Console.WriteLine($"  [FALLO] {resultado.NombreArchivo,-55} {resultado.MensajeError}");

        return informe.TotalFallidos > 0 ? 2 : 0;
    }

    /// <summary>
    /// Vuelca las letras de una pÃ¡gina con el hueco horizontal respecto a la anterior.
    /// Sirve para calibrar cuÃ¡ndo hay que poner un espacio entre palabras.
    /// </summary>
    private static int MostrarLetras(string[] args)
    {
        if (args.Length == 0 || !File.Exists(args[0]))
        {
            Console.WriteLine("Uso: Rpaso.exe --letras <ruta.pdf> [numPagina]");
            return 1;
        }

        var numeroPagina = args.Length > 1 && int.TryParse(args[1], out var n) ? n : 1;

        using var documento = UglyToad.PdfPig.PdfDocument.Open(args[0]);
        var pagina = documento.GetPages().Skip(numeroPagina - 1).FirstOrDefault();

        if (pagina is null)
            return 1;

        Console.WriteLine("Letra   X izq   X der   Hueco   Alto");
        Console.WriteLine(new string('-', 52));

        Letter? anterior = null;
        var impresas = 0;

        foreach (var letra in pagina.Letters.OrderBy(l => l.StartBaseLine.Y).ThenBy(l => l.Location.X))
        {
            if (impresas++ > 300)
                break;

            var hueco = anterior is null ? 0 : letra.Location.X - anterior!.Location.X;
            var alto = letra.BoundingBox.Height;

            if (hueco > 0.05 || anterior is null)
                Console.WriteLine($"{letra.Value,-7} {letra.Location.X,7:F2} {letra.Location.X + letra.Width,7:F2} {hueco,7:F2} {alto,6:F2}");

            anterior = letra;
        }

        return 0;
    }

    public static int Ejecutar(string modo, string[] args) => modo switch
    {
        "--texto" => MostrarTexto(args),
        "--geometria" => MostrarGeometria(args),
        "--analizar" => MostrarAnalisis(args),
        "--importar" => ImportarVarios(args),
        "--letras" => MostrarLetras(args),
        _ => Ejecutar(args)
    };
}

