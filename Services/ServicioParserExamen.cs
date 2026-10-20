using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Rpaso.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Rpaso.Services;

/// <summary>
/// Parser geométrico de exámenes.
/// </summary>
/// <remarks>
/// <para>
/// Trabaja con las coordenadas del texto en lugar de con su cadena, que es lo que permite
/// descartar la publicidad de los pies de página y reconstruir las líneas correctamente.
/// </para>
/// <para>
/// El eje Y de PDF crece hacia arriba: el margen inferior de la página son las Y pequeñas.
/// </para>
/// </remarks>
public sealed class ServicioParserExamen : IServicioParserExamen
{
    /// <summary>Fracción de la altura de página que se descarta por arriba (marca de agua, cabeceras).</summary>
    private const double MargenSuperior = 0.045;

    /// <summary>Fracción de la altura de página que se descarta por abajo (publicidad, avisos legales).</summary>
    private const double MargenInferior = 0.055;

    /// <summary>Tolerancia horizontal para considerar que dos elementos están alineados.</summary>
    private const double ToleranciaHorizontal = 3.0;

    /// <summary>
    /// Etiqueta de opción: "a)", "b.", "c:", "d -". Se admite una viñeta delante
    /// ("• a) texto"), habitual en los PDF de exams.
    /// </summary>
    private static readonly Regex PatronEtiqueta = new(
        @"^\s*[•·▪◦●*]?\s*(?<letra>[a-eA-E])\s*[\)\.\:\-]\s*(?<texto>.+)$",
        RegexOptions.Compiled);

    /// <summary>Número de pregunta: "1.", "12)", "3 -".</summary>
    private static readonly Regex PatronNumero = new(
        @"^\s*[•·▪◦●*]?\s*(?<numero>\d{1,3})\s*[\)\.\:\-]\s*(?<texto>.+)$",
        RegexOptions.Compiled);

    /// <summary>
    /// Par clave de respuestas: "1-b", "2. c", "3) d". La letra no puede ir seguida de ")"
    /// porque eso sería una etiqueta de opción, no una clave.
    /// </summary>
    private static readonly Regex PatronClave = new(
        @"(?<numero>\d{1,3})\s*[\)\.\:\-]\s*(?<letra>[a-eA-E])(?![a-zA-Záéíóúñ\)])",
        RegexOptions.Compiled);

    /// <summary>Palabras que delatan una línea de clave de respuestas.</summary>
    private static readonly Regex PatronPalabraClave = new(
        @"(?i)\b(respuestas?|soluciones?|clave)\b",
        RegexOptions.Compiled);

    /// <summary>Encabezado de bloque: "Pregunta 1", "Question 2". No es una pregunta.</summary>
    private static readonly Regex PatronEncabezado = new(
        @"^\s*(Pregunta|Question)\s*\d{1,3}\s*[:.]?\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Nombres de archivo: sueles ser la portada del PDF, no una pregunta.</summary>
    private static readonly Regex PatronArchivo = new(
        @"\.(pdf|docx?|txt|png|jpe?g|odt|zip|xlsx?|pptx?)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public async Task<Test> AnalizarAsync(string rutaPdf, string carpetaId, CancellationToken ct = default)
    {
        if (!File.Exists(rutaPdf))
            throw new FileNotFoundException("No se encontró el archivo PDF.", rutaPdf);

        var preguntas = new List<Pregunta>();
        var claves = new Dictionary<int, string>();
        var numeros = new Dictionary<string, int>();

        // PdfPig es síncrono: se ejecuta en un hilo de fondo para no bloquear la interfaz.
        await Task.Run(() =>
        {
            using var documento = PdfDocument.Open(rutaPdf);

            foreach (var pagina in documento.GetPages())
            {
                ct.ThrowIfCancellationRequested();

                var lineas = ReconstruirLineas(pagina);
                InterpretarLineas(lineas, preguntas, numeros, claves);
            }
        }, ct);

        AplicarClaves(preguntas, numeros, claves);

        return new Test(
            Id: $"t-{Guid.NewGuid():N}"[..12],
            Titulo: Path.GetFileNameWithoutExtension(rutaPdf),
            CarpetaId: carpetaId,
            FechaImportacion: DateTimeOffset.Now,
            ArchivoOrigen: Path.GetFileName(rutaPdf),
            Preguntas: preguntas);
    }

    // ---------------------------------------------------------------------
    // 1) RECONSTRUCCIÓN DE LÍNEAS A PARTIR DE LAS COORDENADAS
    // ---------------------------------------------------------------------

    private sealed class Linea
    {
        public required List<Word> Palabras { get; init; }
        public required string Texto { get; set; }
        public required double Base { get; init; }
        public required double Izquierda { get; set; }
        public required double Alto { get; init; }
        public Match? Etiqueta { get; set; }
        public Match? Numero { get; set; }

        /// <summary>
        /// X donde empieza el contenido de la línea. En una opción es el texto que va
        /// detrás de la etiqueta ("• a) texto"): la etiqueta y el texto no comparten la X.
        /// Sirve para alinear la continuación de una opción con su texto.
        /// </summary>
        public double XContenido { get; set; }

        /// <summary>
        /// True si alguna palabra de la línea está en negrita. En estos tests la opción
        /// correcta es la única en negrita.
        /// </summary>
        /// <remarks>
        /// Se usa <c>FontDetails.IsBold</c> y no el nombre de la fuente: el prefijo de
        /// subconjunto ("DAAAAA+Times") es asignado por orden de aparición y no dice nada
        /// sobre si la fuente es negrita ("Times" puede ser Roman o Bold).
        /// </remarks>
        public bool Negrita => Palabras.Any(p =>
            p.Letters.Count > 0
            && (p.Letters[0].FontDetails?.IsBold == true
                || p.Letters[0].FontName.Contains("Bold", StringComparison.OrdinalIgnoreCase)
                || p.Letters[0].FontName.Contains("Heavy", StringComparison.OrdinalIgnoreCase)
                || p.Letters[0].FontName.Contains("Black", StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Agrupa las palabras en líneas por su línea base (Y) y descarta los márgenes.
    /// </summary>
    private static List<Linea> ReconstruirLineas(Page pagina)
    {
        var limiteSuperior = pagina.Height * (1 - MargenSuperior);
        var limiteInferior = pagina.Height * MargenInferior;

        var palabras = pagina.GetWords()
            .Where(p => p.TextOrientation == TextOrientation.Horizontal)
            .Where(p => p.BoundingBox.Top <= limiteSuperior)
            .Where(p => p.BoundingBox.Bottom >= limiteInferior)
            .Where(p => !string.IsNullOrWhiteSpace(p.Text))
            .OrderByDescending(p => p.BoundingBox.Bottom)
            .ToList();

        if (palabras.Count == 0)
            return [];

        // Altura de letra típica: referencia para las tolerancias.
        var alturas = palabras.Select(p => p.BoundingBox.Height).Where(h => h > 0).Order().ToList();
        var altoMedio = alturas.Count > 0 ? alturas[alturas.Count / 2] : 8;

        // Dos palabras están en la misma línea si su línea base coincide (± tolerancia).
        var toleranciaLinea = Math.Max(1.5, altoMedio * 0.4);

        var lineas = new List<Linea>();

        foreach (var palabra in palabras)
        {
            var basePalabra = palabra.BoundingBox.Bottom;

            var linea = lineas.FirstOrDefault(l => Math.Abs(l.Base - basePalabra) <= toleranciaLinea);

            if (linea is null)
            {
                linea = new Linea
                {
                    Palabras = [],
                    Texto = string.Empty,
                    Base = basePalabra,
                    Izquierda = palabra.BoundingBox.Left,
                    Alto = altoMedio
                };
                lineas.Add(linea);
            }

            linea.Palabras.Add(palabra);
        }

        foreach (var linea in lineas)
        {
            linea.Palabras.Sort((a, b) => a.BoundingBox.Left.CompareTo(b.BoundingBox.Left));
            linea.Texto = UnirPalabras(linea.Palabras, altoMedio);
            linea.Izquierda = linea.Palabras[0].BoundingBox.Left;
            linea.Etiqueta = PatronEtiqueta.Match(linea.Texto);
            linea.Numero = PatronNumero.Match(linea.Texto);
            linea.XContenido = ObtenerInicioDelContenido(linea) ?? linea.Izquierda;
        }

        // La Y de PDF crece hacia arriba: el orden de lectura de la página es de mayor a menor.
        return lineas.OrderByDescending(l => l.Base).ToList();
    }

    /// <summary>
    /// Devuelve la X donde empieza el texto de una opción (justo detrás de la etiqueta),
    /// o null si la línea no lleva etiqueta.
    /// </summary>
    private static double? ObtenerInicioDelContenido(Linea linea)
    {
        if (linea.Etiqueta is not { Success: true })
            return null;

        var letra = linea.Etiqueta.Groups["letra"].Value;

        for (var i = 0; i < linea.Palabras.Count; i++)
        {
            var palabra = linea.Palabras[i].Text.TrimStart('•', '·', '▪', '◦', '●', '*');

            if (!palabra.StartsWith(letra, StringComparison.OrdinalIgnoreCase))
                continue;

            // La etiqueta puede ser una palabra pegada ("a)") o ir suelta ("A" + ")").
            var siguiente = i + 1;

            while (siguiente < linea.Palabras.Count && EsSoloSeparador(linea.Palabras[siguiente].Text))
                siguiente++;

            if (siguiente < linea.Palabras.Count)
                return linea.Palabras[siguiente].BoundingBox.Left;

            return linea.Palabras[i].BoundingBox.Right;
        }

        return null;
    }

    /// <summary>True si la palabra es solo un separador ("a)", ")", ".", ...).</summary>
    private static bool EsSoloSeparador(string texto)
    {
        var limpia = texto.TrimStart('•', '·', '▪', '◦', '●', '*').Trim();

        return limpia.Length > 0 && limpia.All(c => char.IsPunctuation(c));
    }

    /// <summary>
    /// Une las palabras de una línea. Se mete un espacio si el hueco horizontal entre dos
    /// palabras es mayor que una fracción de la altura de letra.
    /// </summary>
    private static string UnirPalabras(List<Word> palabras, double altoMedio)
    {
        var texto = new StringBuilder();
        Word? anterior = null;

        foreach (var palabra in palabras)
        {
            if (anterior is not null)
            {
                var hueco = palabra.BoundingBox.Left - anterior.BoundingBox.Right;

                if (hueco > altoMedio * 0.15)
                    texto.Append(' ');
            }

            texto.Append(palabra.Text);
            anterior = palabra;
        }

        return texto.ToString().Trim();
    }

    // ---------------------------------------------------------------------
    // 2) INTERPRETACIÓN DE LAS LÍNEAS
    // ---------------------------------------------------------------------

    private enum TipoLinea
    {
        Enunciado,
        Opcion,
        Continuacion,
        Encabezado
    }

    private sealed class LineaClasificada
    {
        public required Linea Linea { get; init; }
        public required TipoLinea Tipo { get; init; }
        public string? Letra { get; init; }
    }

    /// <summary>
    /// Clasifica cada línea de la página como enunciado, opción o continuación de la anterior.
    /// </summary>
    private static List<LineaClasificada> ClasificarLineas(List<Linea> lineas)
    {
        var resultado = new List<LineaClasificada>();

        if (lineas.Count == 0)
            return resultado;

        var xPregunta = lineas.Min(l => l.Izquierda);
        var xOpcion = DetectarColumnaDeOpciones(lineas, xPregunta);

        // Saltos verticales: distinguen "continuación de una opción" de "pregunta nueva".
        var saltos = new List<double>();
        for (var i = 1; i < lineas.Count; i++)
            saltos.Add(lineas[i - 1].Base - lineas[i].Base);

        var saltoMedio = saltos.Count > 0 ? saltos.Order().ToList()[saltos.Count / 2] : 12;
        var umbralContinuacion = saltoMedio * 1.6;

        var anterior = (Linea?)null;
        var anteriorEraOpcion = false;
        var anteriorTerminaba = false;

        foreach (var linea in lineas)
        {
            if (linea.Texto.Length == 0)
                continue;

            var salto = anterior is null ? 0 : anterior.Base - linea.Base;

            // 0) Un encabezado de bloque ("Pregunta 1") no es una pregunta: se ignora,
            //    pero no corta la pregunta anterior ni abre una nueva.
            if (PatronEncabezado.IsMatch(linea.Texto))
            {
                resultado.Add(new LineaClasificada { Linea = linea, Tipo = TipoLinea.Encabezado });
                anterior = linea;
                anteriorEraOpcion = false;
                anteriorTerminaba = true;
                continue;
            }

            // 1) Una etiqueta inequívoca ("a)", "b.") siempre es una opción nueva.
            if (linea.Etiqueta is { Success: true })
            {
                var letra = linea.Etiqueta.Groups["letra"].Value.ToLowerInvariant();
                resultado.Add(new LineaClasificada { Linea = linea, Tipo = TipoLinea.Opcion, Letra = letra });

                anterior = linea;
                anteriorEraOpcion = true;
                anteriorTerminaba = TerminaEnPuntuacion(linea.Texto);
                continue;
            }

            // La continuación de una opción puede ir alineada con la viñeta, con la etiqueta o
            // con el texto (sangrado colgante), así que se comprueba un RANGO, no una igualdad.
            var dentroDeRango = anterior is not null
                && linea.Izquierda >= anterior.Izquierda - ToleranciaHorizontal
                && linea.Izquierda <= anterior.XContenido + ToleranciaHorizontal;

            // Si la línea anterior ya terminó en puntuación, su frase está completa:
            // lo que viene detrás no puede ser su continuación.
            var esContinuacion = dentroDeRango
                && !anteriorTerminaba
                && salto <= umbralContinuacion
                && (anteriorEraOpcion || !EsOpcionPorSangria(linea, xOpcion, xPregunta));

            if (esContinuacion)
            {
                resultado.Add(new LineaClasificada { Linea = linea, Tipo = TipoLinea.Continuacion });
            }
            else if (EsOpcionPorSangria(linea, xOpcion, xPregunta))
            {
                resultado.Add(new LineaClasificada { Linea = linea, Tipo = TipoLinea.Opcion, Letra = "" });
            }
            else
            {
                resultado.Add(new LineaClasificada { Linea = linea, Tipo = TipoLinea.Enunciado });
            }

            anterior = linea;
            anteriorEraOpcion = false;
            anteriorTerminaba = TerminaEnPuntuacion(linea.Texto);
        }

        return resultado;
    }

    /// <summary>
    /// True si la línea está sangrada respecto a la columna de las preguntas: eso significa
    /// que es una opción aunque no lleve letra.
    /// </summary>
    private static bool EsOpcionPorSangria(Linea linea, double xOpcion, double xPregunta)
        => linea.Izquierda >= xOpcion - ToleranciaHorizontal && linea.Izquierda > xPregunta + 2;

    /// <summary>
    /// True si el texto termina en puntuación: la frase está completa, así que lo que viene
    /// detrás no es su continuación. La coma NO cuenta: una frase puede continuar tras coma.
    /// </summary>
    private static bool TerminaEnPuntuacion(string texto)
    {
        var recortado = texto.TrimEnd();

        return recortado.Length > 0
            && (recortado[^1] is '.' or '?' or '!' or ':' or ';');
    }

    /// <summary>
    /// Deduce en qué columna X empiezan las opciones. Si hay opciones con letra, se usa su
    /// posición; si no, se busca el siguiente nivel de sangría a la derecha.
    /// </summary>
    private static double DetectarColumnaDeOpciones(List<Linea> lineas, double xPregunta)
    {
        var conEtiqueta = lineas
            .Where(l => l.Etiqueta is { Success: true })
            .Select(l => l.XContenido)
            .Order()
            .ToList();

        if (conEtiqueta.Count > 0)
            return conEtiqueta[conEtiqueta.Count / 2];

        // Sin etiquetas: el siguiente nivel de X distinto al de las preguntas.
        var niveles = lineas
            .Select(l => Math.Round(l.Izquierda))
            .Distinct()
            .Where(x => x > xPregunta + 2)
            .Order()
            .ToList();

        return niveles.Count > 0 ? niveles[0] : double.MaxValue;
    }

    private sealed class OpcionEnCurso
    {
        public required string Letra { get; init; }
        public required string Texto { get; set; }
        public bool Negrita { get; set; }
    }

    private sealed class PreguntaEnCurso
    {
        public required string Enunciado { get; set; }
        public required List<OpcionEnCurso> Opciones { get; init; }
        public int Numero { get; set; } = -1;
    }

    private static void InterpretarLineas(
        List<Linea> lineas,
        List<Pregunta> preguntas,
        Dictionary<string, int> numeros,
        Dictionary<int, string> claves)
    {
        if (lineas.Count == 0)
            return;

        PreguntaEnCurso? actual = null;

        foreach (var clasificada in ClasificarLineas(lineas))
        {
            var linea = clasificada.Linea;

            RecogerClave(linea, claves);

            // Los encabezados ("Pregunta 1") se ignoran: no abren ni cierran preguntas.
            if (clasificada.Tipo == TipoLinea.Encabezado)
                continue;

            if (clasificada.Tipo == TipoLinea.Opcion)
            {
                var indice = actual?.Opciones.Count ?? 0;
                var (letra, texto) = ExtraerOpcion(linea, indice);

                if (letra.Length > 0)
                    actual?.Opciones.Add(new OpcionEnCurso
                    {
                        Letra = letra,
                        Texto = texto,
                        Negrita = linea.Negrita
                    });

                continue;
            }

            if (clasificada.Tipo == TipoLinea.Continuacion && actual is not null)
            {
                if (actual.Opciones.Count > 0)
                {
                    var ultima = actual.Opciones[^1];
                    ultima.Texto = (ultima.Texto + " " + linea.Texto).Trim();
                    ultima.Negrita |= linea.Negrita;
                }
                else
                {
                    // Continuación del enunciado (que estaba partido en dos líneas).
                    actual.Enunciado = (actual.Enunciado + " " + linea.Texto).Trim();
                }

                continue;
            }

            // Pregunta nueva: se cierra la anterior.
            if (actual is not null)
                CerrarPregunta(actual, preguntas, numeros);

            var numero = -1;
            var enunciado = linea.Texto;

            if (linea.Numero is { Success: true }
                && int.TryParse(linea.Numero.Groups["numero"].Value, out var n))
            {
                numero = n;
                enunciado = linea.Numero.Groups["texto"].Value.Trim();
            }

            // Algunos PDF dejan un punto suelto antes del enunciado.
            enunciado = Regex.Replace(enunciado, @"^\.\s+", "").Trim();

            actual = new PreguntaEnCurso
            {
                Enunciado = enunciado,
                Opciones = [],
                Numero = numero
            };
        }

        if (actual is not null)
            CerrarPregunta(actual, preguntas, numeros);
    }

    private static (string Letra, string Texto) ExtraerOpcion(Linea linea, int indice)
    {
        if (linea.Etiqueta is { Success: true })
        {
            var letra = linea.Etiqueta.Groups["letra"].Value.ToLowerInvariant();
            return (letra, linea.Etiqueta.Groups["texto"].Value.Trim());
        }

        // Opción sin letra: se le asigna la siguiente por orden.
        var siguiente = (char)('a' + Math.Min(indice, 4));
        return (siguiente.ToString(), linea.Texto);
    }

    private static void CerrarPregunta(
        PreguntaEnCurso enCurso,
        List<Pregunta> preguntas,
        Dictionary<string, int> numeros)
    {
        if (enCurso.Enunciado.Length < 8 || enCurso.Opciones.Count < 2)
            return;

        // La portada del PDF (nombre del archivo, título, autoría) no es una pregunta.
        if (PatronArchivo.IsMatch(enCurso.Enunciado.Trim()))
            return;

        var id = $"q-{preguntas.Count + 1}";

        // La clave de respuesta: si hay una clave numerada en el examen se aplica después.
        // Si no, la usa la negrita: en estos tests la opción correcta es la única en negrita.
        var clave = enCurso.Opciones[0].Letra;
        var negritas = enCurso.Opciones.Where(o => o.Negrita).ToList();

        if (negritas.Count == 1)
            clave = negritas[0].Letra;

        preguntas.Add(new Pregunta(
            id,
            enCurso.Enunciado,
            enCurso.Opciones.Select(o => new Opcion(o.Letra, o.Texto)).ToList(),
            clave));

        if (enCurso.Numero > 0)
            numeros[id] = enCurso.Numero;
    }

    /// <summary>
    /// Extrae las claves de respuesta de una línea del tipo "1-b, 2-c" o "Respuestas: 1. b".
    /// </summary>
    private static void RecogerClave(Linea linea, Dictionary<int, string> claves)
    {
        if (linea.Texto.Length > 400)
            return;

        var esLineaDeClave = PatronPalabraClave.IsMatch(linea.Texto);
        var esCorta = linea.Texto.Length <= 60;

        if (!esLineaDeClave && !esCorta)
            return;

        foreach (Match coincidencia in PatronClave.Matches(linea.Texto))
        {
            var numero = int.Parse(coincidencia.Groups["numero"].Value, CultureInfo.InvariantCulture);
            var letra = coincidencia.Groups["letra"].Value.ToLowerInvariant();

            claves[numero] = letra;
        }
    }

    /// <summary>
    /// Aplica las claves de respuesta a las preguntas que tienen número en el examen.
    /// </summary>
    private static void AplicarClaves(
        List<Pregunta> preguntas,
        Dictionary<string, int> numeros,
        Dictionary<int, string> claves)
    {
        for (var i = 0; i < preguntas.Count; i++)
        {
            var pregunta = preguntas[i];

            if (!numeros.TryGetValue(pregunta.Id, out var numero))
                continue;

            if (!claves.TryGetValue(numero, out var letra))
                continue;

            // La clave solo se aplica si apunta a una opción que existe.
            if (!pregunta.Opciones.Any(o => string.Equals(o.Id, letra, StringComparison.OrdinalIgnoreCase)))
                continue;

            preguntas[i] = pregunta with { ClaveRespuesta = letra };
        }
    }

    /// <summary>
    /// Volca la clasificación de una página: cada línea con su posición y el papel que se le
    /// ha asignado. Sirve para entender por qué una pregunta se detecta o no.
    /// </summary>
    public List<(double Base, double Izquierda, double XContenido, string Texto, string Tipo, string? Letra, bool Negrita)>
        DiagnosticarPagina(string rutaPdf, int numeroPagina)
    {
        using var documento = PdfDocument.Open(rutaPdf);
        var pagina = documento.GetPages().Skip(numeroPagina - 1).FirstOrDefault();

        if (pagina is null)
            return [];

        var lineas = ReconstruirLineas(pagina);

        var xPregunta = lineas.Count > 0 ? lineas.Min(l => l.Izquierda) : 0;
        var xOpcion = lineas.Count > 0 ? DetectarColumnaDeOpciones(lineas, xPregunta) : 0;

        Console.WriteLine($"Columna de preguntas X = {xPregunta:F1} | columna de opciones X = {xOpcion:F1}");

        return ClasificarLineas(lineas)
            .Select(c => (
                c.Linea.Base,
                c.Linea.Izquierda,
                c.Linea.XContenido,
                c.Linea.Texto,
                c.Tipo.ToString(),
                c.Letra,
                c.Linea.Negrita))
            .ToList();
    }
}
