using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Rpaso.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;

namespace Rpaso.Services;

/// <summary>
/// Parser geométrico de exámenes (PDF nativo + OCR híbrido).
/// </summary>
public sealed class ServicioParserExamen : IServicioParserExamen
{
    private const double MargenSuperior = 0.045;
    private const double MargenInferior = 0.055;
    private const double ToleranciaHorizontal = 3.5;
    private const double VentanaContinuacion = 85;

    /// <summary>
    /// Etiqueta de opción: soporta "a)", "b.", "c:", "d -", "(a)", "[b]", "a.-" y asterisco de correcta "*a)".
    /// </summary>
    private static readonly Regex PatronEtiqueta = new(
        @"^\s*[•·▪◦●]?\s*(?<asterisco>\*)?\s*[\(\[]?(?<letra>[a-eA-E])[\)\]\.\:\-]+(?:\s*[\.\-])?\s*(?<texto>.+)$",
        RegexOptions.Compiled);

    /// <summary>
    /// Detecta si en una misma línea vienen varias opciones seguidas: "a) Sí   b) No".
    /// </summary>
    private static readonly Regex PatronOpcionesMultiplesEnLinea = new(
        @"(?<=\s{2,})(?=[•·▪◦●*]?\s*[\(\[]?[a-eA-E][\)\]\.\:\-]+\s+\S)",
        RegexOptions.Compiled);

    /// <summary>
    /// Número de pregunta: "1.", "12)", "3 -", "1.-", "Pregunta 4: Texto", e incluso fallos OCR al inicio como "l)" o "I.".
    /// </summary>
    private static readonly Regex PatronNumero = new(
        @"^\s*[•·▪◦●*]?\s*(?:Pregunta\s+|Question\s+)?(?<numero>\d{1,3}|[lI])\s*[\)\.\:\-]+(?:\s*[\.\-])?\s*(?<texto>.+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Par clave de respuestas: "1-b", "2. c", "3) d", "4:a".</summary>
    private static readonly Regex PatronClave = new(
        @"\b(?<numero>\d{1,3})\s*[\)\.\:\-]\s*(?<letra>[a-eA-E])(?![a-zA-ZáéíóúñÁÉÍÓÚÑ\)])",
        RegexOptions.Compiled);

    /// <summary>Palabras que delatan una línea de clave de respuestas.</summary>
    private static readonly Regex PatronPalabraClave = new(
        @"(?i)^\s*(respuestas?|soluciones?|claves?|plantilla)\b",
        RegexOptions.Compiled);

    /// <summary>
    /// Línea explícita de retroalimentación de Campus Virtual / Moodle:
    /// "La respuesta correcta es: b) Texto" o "Respuesta correcta: La opción A".
    /// </summary>
    private static readonly Regex PatronRetroalimentacionRespuesta = new(
        @"^\s*(?:La\s+)?respuesta\s+correcta\s+(?:es\s*)?[:\-]?\s*(?<contenido>.+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Encabezado o metadato de bloque de Campus Virtual / Wuolah.</summary>
    private static readonly Regex PatronEncabezado = new(
        @"^\s*(?:(?:Pregunta|Question)\s*\d{1,3}\s*[:.]?|Seleccione\s+una(?:\s+o\s+m[aá]s\s+de\s+una)?\s*:?|Se\s+punt[uú]a\s+[\d,.]+\s+sobre\s+[\d,.]+|Punt[uú]a\s+como\s+[\d,.]+|Marcar\s+pregunta|Texto\s+de\s+la\s+pregunta|P[aá]gina\s+\d+(?:\s+de\s+\d+)?|Reservados\s+todos\s+los\s+derechos.*|Descargado\s+por\s+.*|Wuolah\s+Free.*)\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Estado de la pregunta en un test ya corregido.</summary>
    private static readonly Regex PatronEstado = new(
        @"^\s*(INCORRECT[OA]|CORRECT[OA]|Acierto|Fallo|Sin\s+contestar|Parcialmente\s+correct[oa]|Finalizado)\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex PatronEtiquetaEstadoExamen = new(
        @"(?i)^\s*(?:opci[oó]n\s+m[uú]ltiple|calificaci[oó]n\s+final|formativa|correct[oa]|incorrect[oa]|acierto|fallo|sin\s+contestar|finalizado)\s*$");

    private static readonly Regex PatronNumeroCabeceraCorregida = new(
        @"^\s*(?<numero>\d{1,3})\s+",
        RegexOptions.Compiled);

    private static readonly Regex PatronMetadatoCabeceraCorregida = new(
        @"(?i)^\s*(?:opci[oó]n\s+m[uú]ltiple|calificaci[oó]n\s+final|formativa|correct[oa]|incorrect[oa]|acierto|fallo|sin\s+contestar|finalizado)\b\s*",
        RegexOptions.Compiled);

    private static readonly Regex PatronPuntuacionCabeceraCorregida = new(
        @"(?i)^\s*(?:(?:[0o]\s*)?/\s*\d+(?:[,.]\d+)?|\d+(?:[,.]\d+)?\s*/\s*\d+(?:[,.]\d+)?)\s+",
        RegexOptions.Compiled);

    /// <summary>Puntuación de una pregunta corregida: "0/ 2,5", "2,5 / 2,5".</summary>
    private static readonly Regex PatronPuntuacion = new(
        @"^\s*\d+(?:[.,]\d+)?\s*/\s*\d+(?:[.,]\d+)?\s*$",
        RegexOptions.Compiled);

    /// <summary>Marca que aparece en el margen derecho de la opción correcta: "Respuesta correcta" o "✓".</summary>
    private static readonly Regex PatronMarcaRespuesta = new(
        @"(?i)(\bRespuesta\s+correcta\b|[✓✔☑])",
        RegexOptions.Compiled);

    /// <summary>Ruido que el OCR deja pegado al texto.</summary>
    private static readonly Regex PatronRuidoOcr = new(
        @"(?i)\b(enviado|confirmaci[oó]n)\b|"
        + @"[0-9]{1,2}/[0-9]{1,2}/[0-9]{2,4}|"
        + @"[0-9]{1,2}:[0-9]{2}(:[0-9]{2})?|"
        + @"[*][0-9A-Fa-f]{8,}",
        RegexOptions.Compiled);

    /// <summary>Prefijo que el PDF pone delante de la opción: "Correcta:" o "Incorrecta:".</summary>
    private static readonly Regex PatronPrefijoVeredicto = new(
        @"^\s*(?<veredicto>Correct[oa]|Incorrect[oa])\s*:\s*",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Botón de radio o casilla al inicio de la opción.</summary>
    private static readonly Regex PatronRadio = new(
        @"^\s*[○◉●⦿⭘◯⭑•☐☒]\s*",
        RegexOptions.Compiled);

    /// <summary>La línea termina con la etiqueta de veredicto: "Correcta:" / "Incorrecta:".</summary>
    private static readonly Regex PatronVeredictoFinal = new(
        @"(?i)\b(correct[oa]|incorrect[oa])\s*:\s*$",
        RegexOptions.Compiled);

    /// <summary>Nombres de archivo en portadas.</summary>
    private static readonly Regex PatronArchivo = new(
        @"\.(pdf|docx?|txt|png|jpe?g|odt|zip|xlsx?|pptx?)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly IServicioOcr? _ocr;

    public ServicioParserExamen(IServicioOcr? ocr = null)
    {
        _ocr = ocr;
    }

    public IReadOnlyList<(double BaseLinea, double Izquierda, double XContenido, string Texto, string Tipo, string? Letra, bool Negrita)>
        DiagnosticarPagina(string rutaPdf, int numeroPagina)
    {
        if (numeroPagina < 1)
            throw new ArgumentOutOfRangeException(nameof(numeroPagina), "El número de página debe ser mayor que cero.");

        using var documento = PdfDocument.Open(rutaPdf);
        if (numeroPagina > documento.NumberOfPages)
            throw new ArgumentOutOfRangeException(nameof(numeroPagina), "El PDF no contiene esa página.");

        var lineas = ReconstruirLineas(documento.GetPage(numeroPagina));
        return ClasificarLineas(lineas, new EstadoClasificacion())
            .Select(clasificada => (
                clasificada.Linea.Base,
                clasificada.Linea.Izquierda,
                clasificada.Linea.XContenido,
                clasificada.Linea.Texto,
                clasificada.Tipo.ToString(),
                clasificada.Letra,
                clasificada.Linea.Negrita))
            .ToList();
    }

    public async Task<IReadOnlyList<(double BaseLinea, double Izquierda, double XContenido, string Texto, string Tipo, string? Letra, bool Negrita, bool Resaltada)>>
        DiagnosticarOcrAsync(string rutaPdf, int numeroPagina, CancellationToken ct = default)
    {
        if (numeroPagina < 1)
            throw new ArgumentOutOfRangeException(nameof(numeroPagina), "El número de página debe ser mayor que cero.");
        if (_ocr is not { Disponible: true })
            throw new InvalidOperationException(_ocr?.MotivoIndisponible ?? "No se ha configurado un servicio OCR disponible.");

        var paginas = await _ocr.ReconocerAsync(rutaPdf, ct);
        if (numeroPagina > paginas.Count)
            throw new ArgumentOutOfRangeException(nameof(numeroPagina), "El PDF no contiene esa página.");

        var pagina = paginas[numeroPagina - 1];
        var lineas = ReconstruirLineas(pagina.Palabras.ToList(), pagina.Ancho, pagina.Alto);
        return ClasificarLineas(lineas, new EstadoClasificacion())
            .Select(clasificada => (
                clasificada.Linea.Base,
                clasificada.Linea.Izquierda,
                clasificada.Linea.XContenido,
                clasificada.Linea.Texto,
                clasificada.Tipo.ToString(),
                clasificada.Letra,
                clasificada.Linea.Negrita,
                clasificada.Linea.Resaltada))
            .ToList();
    }

    public async Task<AnalisisExamen> AnalizarAsync(string rutaPdf, string carpetaId, CancellationToken ct = default)
    {
        if (!File.Exists(rutaPdf))
            throw new FileNotFoundException("No se encontró el archivo PDF.", rutaPdf);

        var preguntas = new List<Pregunta>();
        var claves = new Dictionary<int, string>();
        var numeros = new Dictionary<string, int>();
        var paginasConTexto = 0;
        var paginasTotales = 0;
        var lineasTotales = 0;

        var fiables = new HashSet<string>(StringComparer.Ordinal);
        var clasificacion = new EstadoClasificacion();
        var interpretacion = new EstadoInterpretacion();

        // Guardamos los resaltados vectoriales por página por si el documento es escaneado
        // pero tiene subrayados digitales en amarillo hechos con un visor de PDF.
        var resaltadosPorPagina = new List<IReadOnlyList<Resaltado>>();

        await Task.Run(() =>
        {
            using var documento = PdfDocument.Open(rutaPdf);

            foreach (var pagina in documento.GetPages())
            {
                ct.ThrowIfCancellationRequested();

                paginasTotales++;

                var resaltados = ExtraerResaltados(pagina);
                resaltadosPorPagina.Add(resaltados);

                var lineas = ReconstruirLineas(ExtraerPalabras(pagina), pagina.Width, pagina.Height, resaltados);

                if (lineas.Count > 0)
                    paginasConTexto++;

                lineasTotales += lineas.Count;
                InterpretarLineas(lineas, clasificacion, interpretacion, preguntas, numeros, claves, fiables,
                    unirFragmentosOcr: false);
            }
        }, ct);

        if (interpretacion.Actual is not null)
        {
            CerrarPregunta(interpretacion.Actual, preguntas, numeros, fiables, unirFragmentosOcr: false);
            interpretacion.Actual = null;
        }

        // Si la capa de texto no produjo ninguna pregunta (ej. era un escaneo o solo tenía
        // texto vectorial de publicidad de Wuolah en los márgenes), lanzamos el OCR.
        var usoOcr = false;

        if (preguntas.Count == 0 && _ocr is { Disponible: true })
        {
            claves.Clear();
            numeros.Clear();
            fiables.Clear();
            clasificacion = new EstadoClasificacion();
            interpretacion = new EstadoInterpretacion();
            paginasConTexto = 0;
            lineasTotales = 0;

            var paginasOcr = await _ocr.ReconocerAsync(rutaPdf, ct);

            for (var i = 0; i < paginasOcr.Count; i++)
            {
                ct.ThrowIfCancellationRequested();

                var pagina = paginasOcr[i];
                var resaltados = i < resaltadosPorPagina.Count ? resaltadosPorPagina[i] : null;

                var lineas = ReconstruirLineas(pagina.Palabras.ToList(), pagina.Ancho, pagina.Alto, resaltados);

                if (lineas.Count > 0)
                    paginasConTexto++;

                lineasTotales += lineas.Count;
                InterpretarLineas(lineas, clasificacion, interpretacion, preguntas, numeros, claves, fiables,
                    unirFragmentosOcr: true);
            }

            if (interpretacion.Actual is not null)
            {
                CerrarPregunta(interpretacion.Actual, preguntas, numeros, fiables, unirFragmentosOcr: true);
                interpretacion.Actual = null;
            }

            usoOcr = paginasOcr.Count > 0;
        }

        AplicarClaves(preguntas, numeros, claves, fiables);

        var descartadas = 0;

        if (fiables.Count < preguntas.Count)
        {
            var conservadas = new List<Pregunta>(preguntas.Count);

            foreach (var pregunta in preguntas)
            {
                if (fiables.Contains(pregunta.Id))
                    conservadas.Add(pregunta);
                else
                    descartadas++;
            }

            preguntas = conservadas;
        }

        var test = new Test(
            Id: $"t-{Guid.NewGuid():N}"[..12],
            Titulo: Path.GetFileNameWithoutExtension(rutaPdf),
            CarpetaId: carpetaId,
            FechaImportacion: DateTimeOffset.Now,
            ArchivoOrigen: Path.GetFileName(rutaPdf),
            Preguntas: preguntas);

        var aviso = CalcularAviso(paginasTotales, paginasConTexto, lineasTotales, preguntas.Count, descartadas);

        if (usoOcr && aviso is not null)
            aviso = "El PDF es un escaneo y se ha leído con OCR: " + aviso;

        return new AnalisisExamen(test, aviso, descartadas);
    }

    private static string? CalcularAviso(
        int paginasTotales, int paginasConTexto, int lineasTotales, int preguntas, int descartadas)
    {
        if (preguntas > 0)
            return null;

        if (descartadas > 0)
        {
            return descartadas == 1
                ? "Se ha descartado 1 pregunta porque el PDF no indica cuál es la respuesta correcta."
                : $"Se han descartado {descartadas} preguntas porque el PDF no indica cuál es la respuesta correcta.";
        }

        if (paginasTotales == 0)
            return "El PDF no tiene páginas.";

        if (paginasConTexto == 0)
            return "Es un PDF escaneado: no tiene capa de texto (necesita OCR).";

        if (lineasTotales == 0)
            return "El texto está fuera del área imprimible de la página.";

        return $"Se han leído {lineasTotales} líneas pero ninguna forma una pregunta con sus opciones.";
    }

    // ---------------------------------------------------------------------
    // 1) RECONSTRUCCIÓN DE LÍNEAS A PARTIR DE LAS COORDENADAS
    // ---------------------------------------------------------------------

    private sealed class Linea
    {
        public required List<PalabraTexto> Palabras { get; init; }
        public required string Texto { get; set; }
        public required double Base { get; init; }
        public required double Izquierda { get; set; }
        public required double Alto { get; init; }
        public Match? Etiqueta { get; set; }
        public Match? Numero { get; set; }
        public double XContenido { get; set; }

        public bool ContieneMarcaRespuesta => PatronMarcaRespuesta.IsMatch(Texto);
        public bool Resaltada { get; set; }
        public bool MarcaCorrecta { get; set; }

        public double Superior => Palabras.Count == 0 ? Base : Palabras.Max(p => p.Superior);
        public double Inferior => Palabras.Count == 0 ? Base : Palabras.Min(p => p.Inferior);
        public double Derecha => Palabras.Count == 0 ? Izquierda : Palabras.Max(p => p.Derecha);

        /// <summary>
        /// True si el cuerpo de la línea (ignorando la etiqueta inicial "a)", "1.") está mayoritariamente
        /// en negrita. Evita falsos positivos cuando el PDF pone las letras "a)", "b)" en negrita en todas las opciones.
        /// </summary>
        public bool Negrita
        {
            get
            {
                if (Palabras.Count == 0)
                    return false;

                var inicio = 0;
                if (Etiqueta is { Success: true } && Palabras.Count > 1)
                {
                    inicio = 1;
                    while (inicio < Palabras.Count - 1 && EsSoloSeparador(Palabras[inicio].Texto))
                        inicio++;
                }

                var totalCuerpo = Palabras.Count - inicio;
                if (totalCuerpo <= 0)
                    return Palabras[0].Negrita;

                var negritasCuerpo = 0;
                for (var i = inicio; i < Palabras.Count; i++)
                {
                    if (Palabras[i].Negrita)
                        negritasCuerpo++;
                }

                return (double)negritasCuerpo / totalCuerpo >= 0.6;
            }
        }
    }

    private static List<PalabraTexto> ExtraerPalabras(Page pagina) =>
        pagina.GetWords(new NearestNeighbourWordExtractor(
            new NearestNeighbourWordExtractor.NearestNeighbourWordExtractorOptions
            {
                MaximumDistance = DistanciaMaximaPalabra
            }))
            .Where(p => p.TextOrientation == TextOrientation.Horizontal)
            .Where(p => !string.IsNullOrWhiteSpace(p.Text))
            .Select(p => new PalabraTexto(
                ServicioExtraccionPdf.NormalizarTextoPdf(p.Text),
                p.BoundingBox.Left,
                p.BoundingBox.Right,
                p.BoundingBox.Top,
                p.BoundingBox.Bottom,
                EsNegrita(p)))
            .Where(p => p.Texto.Length > 0)
            .OrderByDescending(p => p.Inferior)
            .ToList();

    private static bool EsNegrita(Word palabra)
    {
        if (palabra.Letters.Count == 0)
            return false;

        // Evaluamos si más de la mitad de las letras de la palabra son negrita
        var negritas = 0;
        foreach (var letra in palabra.Letters)
        {
            var bold = letra.FontDetails?.IsBold == true
                || (letra.FontName?.Contains("Bold", StringComparison.OrdinalIgnoreCase) ?? false)
                || (letra.FontName?.Contains("Heavy", StringComparison.OrdinalIgnoreCase) ?? false)
                || (letra.FontName?.Contains("Black", StringComparison.OrdinalIgnoreCase) ?? false)
                || (letra.FontName?.Contains("Medi", StringComparison.OrdinalIgnoreCase) ?? false);

            if (bold)
                negritas++;
        }

        return negritas * 2 >= palabra.Letters.Count;
    }

    private static List<Linea> ReconstruirLineas(Page pagina)
        => ReconstruirLineas(
            ExtraerPalabras(pagina), pagina.Width, pagina.Height, ExtraerResaltados(pagina));

    private static List<Linea> ReconstruirLineas(
        List<PalabraTexto> palabras,
        double anchoPagina,
        double altoPagina,
        IReadOnlyList<Resaltado>? resaltados = null)
    {
        var limiteSuperior = altoPagina * (1 - MargenSuperior);
        var limiteInferior = altoPagina * MargenInferior;

        palabras = palabras
            .Where(p => p.Superior <= limiteSuperior)
            .Where(p => p.Inferior >= limiteInferior)
            .OrderByDescending(p => p.Inferior)
            .ToList();

        if (palabras.Count == 0)
            return [];

        var alturas = palabras.Select(p => p.Alto).Where(h => h > 0).Order().ToList();
        var altoMedio = alturas.Count > 0 ? alturas[alturas.Count / 2] : 8;

        var toleranciaLinea = Math.Max(2.2, altoMedio * 0.55);
        var huecoMaximo = Math.Max(55, anchoPagina * 0.10);

        var bandas = new List<List<PalabraTexto>>();
        var basesBanda = new List<double>();

        foreach (var palabra in palabras)
        {
            var indice = -1;
            var mejorDiferencia = double.MaxValue;

            for (var i = 0; i < bandas.Count; i++)
            {
                var diferencia = Math.Abs(basesBanda[i] - palabra.Inferior);

                if (diferencia <= toleranciaLinea && diferencia < mejorDiferencia)
                {
                    mejorDiferencia = diferencia;
                    indice = i;
                }
            }

            if (indice < 0)
            {
                bandas.Add([palabra]);
                basesBanda.Add(palabra.Inferior);
            }
            else
            {
                bandas[indice].Add(palabra);
                // Actualizar media móvil de la base de la banda para líneas ligeramente inclinadas en OCR
                basesBanda[indice] = (basesBanda[indice] * (bandas[indice].Count - 1) + palabra.Inferior) / bandas[indice].Count;
            }
        }

        var lineas = new List<Linea>();

        foreach (var banda in bandas)
        {
            var ordenadas = banda.OrderBy(p => p.Izquierda).ToList();
            var palabrasLinea = new List<PalabraTexto>();

            void CerrarLinea()
            {
                if (palabrasLinea.Count == 0)
                    return;

                var baseLinea = palabrasLinea
                    .Select(p => p.Inferior)
                    .Order()
                    .ElementAt(palabrasLinea.Count / 2);

                lineas.Add(new Linea
                {
                    Palabras = palabrasLinea,
                    Texto = string.Empty,
                    Base = baseLinea,
                    Izquierda = palabrasLinea[0].Izquierda,
                    Alto = altoMedio
                });

                palabrasLinea = [];
            }

            foreach (var palabra in ordenadas)
            {
                // Cortar línea si hay un gran hueco (nota al margen) o si aparece una nueva opción en horizontal ("a) Sí   b) No")
                if (palabrasLinea.Count > 0)
                {
                    var hueco = palabra.Izquierda - palabrasLinea[^1].Derecha;
                    var pareceNuevaOpcionEnFila = hueco >= Math.Max(14, altoMedio * 1.5)
                        && Regex.IsMatch(palabra.Texto, @"^[•·▪◦●*]?\s*[\(\[]?[a-eA-E][\)\]\.\:\-]+$");

                    if (hueco > huecoMaximo || pareceNuevaOpcionEnFila)
                        CerrarLinea();
                }

                palabrasLinea.Add(palabra);
            }

            CerrarLinea();
        }

        var lineasFinales = new List<Linea>(lineas.Count);

        foreach (var linea in lineas)
        {
            linea.Palabras.Sort((a, b) => a.Izquierda.CompareTo(b.Izquierda));
            linea.Texto = UnirPalabras(linea.Palabras, altoMedio);
            linea.Izquierda = linea.Palabras[0].Izquierda;

            if (resaltados is { Count: > 0 })
                linea.Resaltada = resaltados.Any(r => SeSolapan(linea, r));

            // Si en el texto unido siguen viniendo varias opciones en la misma línea ("a) Opción 1   b) Opción 2"), las desdoblamos
            var subPartes = PatronOpcionesMultiplesEnLinea.Split(linea.Texto);
            if (subPartes.Length > 1 && PatronEtiqueta.IsMatch(subPartes[0]))
            {
                for (var idx = 0; idx < subPartes.Length; idx++)
                {
                    var subTexto = subPartes[idx].Trim();
                    if (subTexto.Length == 0) continue;

                    var subLinea = new Linea
                    {
                        Palabras = linea.Palabras,
                        Texto = subTexto,
                        Base = linea.Base - (idx * 0.01), // Mantener orden relativo de lectura
                        Izquierda = linea.Izquierda,
                        Alto = linea.Alto,
                        Resaltada = linea.Resaltada
                    };
                    InicializarMetadatosLinea(subLinea);
                    lineasFinales.Add(subLinea);
                }
            }
            else
            {
                InicializarMetadatosLinea(linea);
                lineasFinales.Add(linea);
            }
        }

        return lineasFinales
            .OrderByDescending(l => Math.Round(l.Base, 1))
            .ThenBy(l => l.Izquierda)
            .ToList();
    }

    private static void InicializarMetadatosLinea(Linea linea)
    {
        linea.Etiqueta = PatronEtiqueta.Match(linea.Texto);
        linea.Numero = PatronNumero.Match(linea.Texto);
        linea.XContenido = ObtenerInicioDelContenido(linea) ?? linea.Izquierda;
    }

    private sealed record Resaltado(double Izquierda, double Derecha, double Superior, double Inferior);

    private static List<Resaltado> ExtraerResaltados(Page pagina)
    {
        var resaltados = new List<Resaltado>();

        try
        {
            foreach (var trazo in pagina.Paths)
            {
                if (!trazo.IsFilled || trazo.FillColor is null)
                    continue;

                var caja = trazo.GetBoundingRectangle();
                if (caja is null)
                    continue;

                var rect = caja.Value;

                var izquierda = Math.Min(rect.Left, rect.Right);
                var derecha = Math.Max(rect.Left, rect.Right);
                var superior = Math.Max(rect.Top, rect.Bottom);
                var inferior = Math.Min(rect.Top, rect.Bottom);

                var ancho = derecha - izquierda;
                var alto = superior - inferior;

                if (ancho < 12 || alto < 4 || alto > 45)
                    continue;

                var (r, g, b) = trazo.FillColor.ToRGBValues();

                // Acepta amarillo, verde claro, naranja suave o cian de subrayado
                var esCalidoOVerde = (r >= 0.55 && g >= 0.55 && b <= 0.85) || (g >= 0.65 && g > r * 1.15 && g > b * 1.15);
                var esMuyClaro = r >= 0.94 && g >= 0.94 && b >= 0.94;

                if (!esCalidoOVerde || esMuyClaro)
                    continue;

                resaltados.Add(new Resaltado(izquierda, derecha, superior, inferior));
            }
        }
        catch
        {
            // Ignorar trazos malformados
        }

        return resaltados;
    }

    private static bool SeSolapan(Linea linea, Resaltado resaltado)
    {
        var altoLinea = linea.Superior - linea.Inferior;
        var anchoLinea = linea.Derecha - linea.Izquierda;

        if (altoLinea <= 0 || anchoLinea <= 0)
            return false;

        var solapeVertical = Math.Min(linea.Superior, resaltado.Superior)
            - Math.Max(linea.Inferior, resaltado.Inferior);

        var solapeHorizontal = Math.Min(linea.Derecha, resaltado.Derecha)
            - Math.Max(linea.Izquierda, resaltado.Izquierda);

        return solapeVertical >= altoLinea * 0.35
            && solapeHorizontal >= Math.Min(anchoLinea * 0.25, 35);
    }

    private static double? ObtenerInicioDelContenido(Linea linea)
    {
        if (linea.Etiqueta is not { Success: true })
            return null;

        var letra = linea.Etiqueta.Groups["letra"].Value;

        for (var i = 0; i < linea.Palabras.Count; i++)
        {
            var palabra = linea.Palabras[i].Texto.TrimStart('•', '·', '▪', '◦', '●', '*', '(', '[');

            if (!palabra.StartsWith(letra, StringComparison.OrdinalIgnoreCase))
                continue;

            var siguiente = i + 1;

            while (siguiente < linea.Palabras.Count && EsSoloSeparador(linea.Palabras[siguiente].Texto))
                siguiente++;

            if (siguiente < linea.Palabras.Count)
                return linea.Palabras[siguiente].Izquierda;

            return linea.Palabras[i].Derecha;
        }

        return null;
    }

    private static bool EsSoloSeparador(string texto)
    {
        var limpia = texto.TrimStart('•', '·', '▪', '◦', '●', '*').Trim();
        return limpia.Length > 0 && limpia.All(char.IsPunctuation);
    }

    private static readonly Func<Letter, Letter, double> DistanciaMaximaPalabra
        = (l1, l2) => Math.Max(0.6, Math.Min(l1.PointSize, l2.PointSize) * 0.12);

    private static string UnirPalabras(List<PalabraTexto> palabras, double altoMedio)
    {
        var texto = new StringBuilder();
        PalabraTexto? anterior = null;

        var umbral = Math.Max(0.35, altoMedio * 0.06);

        foreach (var palabra in palabras)
        {
            if (anterior is not null)
            {
                var hueco = palabra.Izquierda - anterior.Derecha;

                // No meter espacio si es un acento desglosado o signo de cierre pegado
                var esSignoPegado = palabra.Texto is "." or "," or ";" or ":" or ")" or "]" or "?" or "!" or "´" or "¨";

                if (hueco > umbral && !esSignoPegado)
                    texto.Append(' ');
            }

            texto.Append(palabra.Texto);
            anterior = palabra;
        }

        return ServicioExtraccionPdf.NormalizarTextoPdf(texto.ToString());
    }

    // ---------------------------------------------------------------------
    // 2) INTERPRETACIÓN DE LAS LÍNEAS
    // ---------------------------------------------------------------------

    private enum TipoLinea
    {
        Enunciado,
        Opcion,
        Continuacion,
        Encabezado,
        MarcaRespuesta,
        Retroalimentacion
    }

    private sealed class LineaClasificada
    {
        public required Linea Linea { get; init; }
        public required TipoLinea Tipo { get; init; }
        public string? Letra { get; init; }
    }

    private static List<LineaClasificada> ClasificarLineas(List<Linea> lineas, EstadoClasificacion estado)
    {
        var resultado = new List<LineaClasificada>();

        if (lineas.Count == 0)
            return resultado;

        var xPregunta = lineas.Min(l => l.Izquierda);
        var xOpcion = DetectarColumnaDeOpciones(lineas, xPregunta);

        var saltos = new List<double>();
        for (var i = 1; i < lineas.Count; i++)
        {
            var diff = lineas[i - 1].Base - lineas[i].Base;
            if (diff > 1) saltos.Add(diff);
        }

        var saltoMedio = saltos.Count > 0 ? saltos.Order().ToList()[saltos.Count / 2] : 12;
        var umbralContinuacion = saltoMedio * 1.75;
        var primeraPregunta = lineas.FindIndex(l =>
            l.Numero is { Success: true } || EsEtiquetaEstadoExamen(l.Texto));
        var titulosCentrados = new HashSet<int>();

        if (primeraPregunta > 0)
        {
            var izquierdaPagina = lineas.Min(l => l.Izquierda);
            var derechaPagina = lineas.Max(l => l.Derecha);
            var anchoContenido = derechaPagina - izquierdaPagina;
            var centroPagina = (izquierdaPagina + derechaPagina) / 2;

            if (anchoContenido > 0)
            {
                for (var indice = 0; indice < primeraPregunta; indice++)
                {
                    var linea = lineas[indice];
                    var centroLinea = (linea.Izquierda + linea.Derecha) / 2;
                    var anchoLinea = linea.Derecha - linea.Izquierda;

                    if (Math.Abs(centroLinea - centroPagina) <= anchoContenido * 0.08
                        && anchoLinea <= anchoContenido * 0.8)
                    {
                        titulosCentrados.Add(indice);
                    }
                }
            }
        }

        var anterior = estado.Anterior;
        var anteriorEraOpcion = estado.AnteriorEraOpcion;
        var anteriorTerminaba = estado.AnteriorTerminaba;
        var opcionesEnBloque = estado.OpcionesEnBloque;

        for (var indice = 0; indice < lineas.Count; indice++)
        {
            var linea = lineas[indice];

            if (string.IsNullOrWhiteSpace(linea.Texto)
                || linea.Texto.All(char.IsPunctuation))
                continue;

            if (titulosCentrados.Contains(indice))
                continue;

            if (EsEtiquetaEstadoExamen(linea.Texto))
            {
                resultado.Add(new LineaClasificada { Linea = linea, Tipo = TipoLinea.Encabezado });
                continue;
            }

            var primeraDePagina = indice == 0 && anterior is not null;
            var salto = anterior is null ? 0 : anterior.Base - linea.Base;

            var continuacionPermitida = primeraDePagina
                ? EmpiezaEnMinuscula(linea.Texto)
                : salto <= umbralContinuacion;

            // 0a) Retroalimentación explícita de Campus Virtual ("La respuesta correcta es: ...")
            if (PatronRetroalimentacionRespuesta.IsMatch(linea.Texto))
            {
                resultado.Add(new LineaClasificada { Linea = linea, Tipo = TipoLinea.Retroalimentacion });
                anterior = linea;
                anteriorEraOpcion = false;
                anteriorTerminaba = true;
                continue;
            }

            // 0b) Marca "Respuesta correcta" en el margen derecho
            if (linea.Etiqueta is not { Success: true } && PatronMarcaRespuesta.IsMatch(linea.Texto))
            {
                resultado.Add(new LineaClasificada { Linea = linea, Tipo = TipoLinea.MarcaRespuesta });
                anterior = linea;
                anteriorEraOpcion = false;
                anteriorTerminaba = true;
                continue;
            }

            // 0c) Encabezados, estados y puntuaciones
            if (PatronEncabezado.IsMatch(linea.Texto) || PatronEstado.IsMatch(linea.Texto) || PatronPuntuacion.IsMatch(linea.Texto))
            {
                resultado.Add(new LineaClasificada { Linea = linea, Tipo = TipoLinea.Encabezado });
                continue;
            }

            // 1) Etiqueta explícita ("a)", "b."):
            //    Validamos secuencia para que una cita legal en el enunciado ("según el art. c) de la ley")
            //    no se tome como la primera opción si aún no ha salido la 'a'.
            if (linea.Etiqueta is { Success: true })
            {
                var letra = linea.Etiqueta.Groups["letra"].Value.ToLowerInvariant();
                var esPrimeraValida = opcionesEnBloque == 0 && letra == "a";
                var esSiguienteValida = opcionesEnBloque > 0;

                if (esPrimeraValida || esSiguienteValida)
                {
                    resultado.Add(new LineaClasificada { Linea = linea, Tipo = TipoLinea.Opcion, Letra = letra });

                    anterior = linea;
                    anteriorEraOpcion = true;
                    anteriorTerminaba = TerminaEnPuntuacion(linea.Texto);
                    opcionesEnBloque++;
                    continue;
                }
            }

            if (EsLineaDeClaves(linea.Texto))
            {
                resultado.Add(new LineaClasificada { Linea = linea, Tipo = TipoLinea.Encabezado });
                anterior = linea;
                anteriorEraOpcion = false;
                anteriorTerminaba = true;
                continue;
            }

            // Pregunta numerada ("12. ...")
            if (linea.Numero is { Success: true })
            {
                resultado.Add(new LineaClasificada { Linea = linea, Tipo = TipoLinea.Enunciado });
                anterior = linea;
                anteriorEraOpcion = false;
                anteriorTerminaba = TerminaEnPuntuacion(linea.Texto);
                opcionesEnBloque = 0;
                continue;
            }

            var dentroDeRango = anterior is not null
                && linea.Izquierda >= anterior.Izquierda - ToleranciaHorizontal
                && linea.Izquierda <= Math.Max(anterior.XContenido, anterior.Izquierda + VentanaContinuacion);

            // Una línea también continúa a la anterior si aún estamos en el enunciado (opcionesEnBloque == 0)
            // y el salto vertical es normal, incluso si la primera frase del enunciado tenía un punto o dos puntos.
            var esContinuacionDeEnunciado = !anteriorEraOpcion
                && opcionesEnBloque == 0
                && anterior is not null
                && continuacionPermitida
                && (dentroDeRango || !anteriorTerminaba || EmpiezaEnMinuscula(linea.Texto));

            var esContinuacionDeOpcion = anteriorEraOpcion
                && dentroDeRango
                && continuacionPermitida
                && (!anteriorTerminaba || EmpiezaEnMinuscula(linea.Texto));

            if (esContinuacionDeEnunciado || esContinuacionDeOpcion)
            {
                resultado.Add(new LineaClasificada { Linea = linea, Tipo = TipoLinea.Continuacion });
                // CRÍTICO: Conservar anteriorEraOpcion si estábamos continuando una opción multilínea
                anterior = linea;
                anteriorTerminaba = TerminaEnPuntuacion(linea.Texto);
                continue;
            }

            if (primeraDePagina || !EsOpcionPorSangria(linea, xOpcion, xPregunta))
            {
                resultado.Add(new LineaClasificada { Linea = linea, Tipo = TipoLinea.Enunciado });
                anteriorEraOpcion = false;
                opcionesEnBloque = 0;
            }
            else
            {
                resultado.Add(new LineaClasificada { Linea = linea, Tipo = TipoLinea.Opcion, Letra = "" });
                anteriorEraOpcion = true;
                opcionesEnBloque++;
            }

            anterior = linea;
            anteriorTerminaba = TerminaEnPuntuacion(linea.Texto);
        }

        estado.Anterior = anterior;
        estado.AnteriorEraOpcion = anteriorEraOpcion;
        estado.AnteriorTerminaba = anteriorTerminaba;
        estado.OpcionesEnBloque = opcionesEnBloque;

        foreach (var marca in resultado.Where(c => c.Tipo == TipoLinea.MarcaRespuesta))
        {
            var candidata = resultado
                .Where(c => c.Tipo == TipoLinea.Opcion)
                .OrderBy(c => Math.Abs(c.Linea.Base - marca.Linea.Base))
                .FirstOrDefault();

            if (candidata is not null && Math.Abs(candidata.Linea.Base - marca.Linea.Base) <= 10)
                candidata.Linea.MarcaCorrecta = true;
        }

        return resultado;
    }

    private static bool EsOpcionPorSangria(Linea linea, double xOpcion, double xPregunta)
        => linea.Izquierda >= xOpcion - ToleranciaHorizontal && linea.Izquierda > xPregunta + 2;

    private static bool EsEtiquetaEstadoExamen(string texto)
        => PatronEtiquetaEstadoExamen.IsMatch(texto);

    private static bool TerminaEnPuntuacion(string texto)
    {
        var recortado = texto.TrimEnd();

        if (PatronVeredictoFinal.IsMatch(recortado))
            return false;

        return recortado.Length > 0
            && (recortado[^1] is '.' or '?' or '!' or ';');
    }

    private static bool EmpiezaEnMinuscula(string texto)
    {
        foreach (var c in texto)
        {
            if (char.IsLetter(c))
                return char.IsLower(c);

            if (!char.IsWhiteSpace(c))
                return false;
        }

        return false;
    }

    private static bool EsFragmentoDeOpcion(OpcionEnCurso opcion, OpcionEnCurso anterior)
    {
        var texto = opcion.Texto.Trim();

        if (texto.Length == 0 || !char.IsLower(texto[0]))
            return false;

        if (texto.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > 3)
            return false;

        if (anterior.Texto.Trim().Length < 10)
            return false;

        return !TerminaEnPuntuacion(anterior.Texto);
    }

    private static double DetectarColumnaDeOpciones(List<Linea> lineas, double xPregunta)
    {
        var conEtiqueta = lineas
            .Where(l => l.Etiqueta is { Success: true })
            .Select(l => l.XContenido)
            .Order()
            .ToList();

        if (conEtiqueta.Count > 0)
            return conEtiqueta[conEtiqueta.Count / 2];

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
        public required string Letra { get; set; }
        public required string Texto { get; set; }
        public bool Negrita { get; set; }
        public double Base { get; set; }
        public bool EsCorrecta { get; set; }
        public bool EsIncorrecta { get; set; }
    }

    private sealed class PreguntaEnCurso
    {
        public required string Enunciado { get; set; }
        public required List<OpcionEnCurso> Opciones { get; init; }
        public int Numero { get; set; } = -1;
        public string? TextoRetroalimentacion { get; set; }
    }

    private sealed class EstadoClasificacion
    {
        public Linea? Anterior { get; set; }
        public bool AnteriorEraOpcion { get; set; }
        public bool AnteriorTerminaba { get; set; }
        public int OpcionesEnBloque { get; set; }
    }

    private sealed class EstadoInterpretacion
    {
        public PreguntaEnCurso? Actual { get; set; }
    }

    private static void InterpretarLineas(
        List<Linea> lineas,
        EstadoClasificacion clasificacion,
        EstadoInterpretacion interpretacion,
        List<Pregunta> preguntas,
        Dictionary<string, int> numeros,
        Dictionary<int, string> claves,
        HashSet<string> fiables,
        bool unirFragmentosOcr)
    {
        if (lineas.Count == 0)
            return;

        foreach (var clasificada in ClasificarLineas(lineas, clasificacion))
        {
            var linea = clasificada.Linea;

            RecogerClave(linea, claves);

            if (clasificada.Tipo == TipoLinea.Encabezado || clasificada.Tipo == TipoLinea.MarcaRespuesta)
                continue;

            if (clasificada.Tipo == TipoLinea.Retroalimentacion)
            {
                if (interpretacion.Actual is not null)
                {
                    var match = PatronRetroalimentacionRespuesta.Match(linea.Texto);
                    if (match.Success)
                        interpretacion.Actual.TextoRetroalimentacion = match.Groups["contenido"].Value.Trim();
                }
                continue;
            }

            if (clasificada.Tipo == TipoLinea.Opcion)
            {
                if (interpretacion.Actual is null)
                    continue;

                var indice = interpretacion.Actual.Opciones.Count;
                var opcion = ExtraerOpcion(linea, indice, unirFragmentosOcr);

                if (opcion.Letra.Length == 0)
                    continue;

                if (linea.ContieneMarcaRespuesta || linea.MarcaCorrecta)
                    opcion.EsCorrecta = true;

                interpretacion.Actual.Opciones.Add(opcion);
                continue;
            }

            if (clasificada.Tipo == TipoLinea.Continuacion && interpretacion.Actual is not null)
            {
                if (interpretacion.Actual.Opciones.Count > 0)
                {
                    var ultimaOpcion = interpretacion.Actual.Opciones[^1];
                    var fragmento = ExtraerOpcion(linea, interpretacion.Actual.Opciones.Count - 1, unirFragmentosOcr);

                    if (unirFragmentosOcr
                        && fragmento.Letra.Length > 0
                        && EsFragmentoDeOpcion(fragmento, ultimaOpcion))
                    {
                        ultimaOpcion.Texto = UnirTexto(ultimaOpcion.Texto, fragmento.Texto);
                        ultimaOpcion.EsCorrecta |= fragmento.EsCorrecta;
                    }
                    else
                    {
                        ultimaOpcion.Texto = UnirTexto(ultimaOpcion.Texto, linea.Texto);
                        ultimaOpcion.EsCorrecta |= linea.ContieneMarcaRespuesta || linea.MarcaCorrecta;
                    }
                }
                else
                {
                    interpretacion.Actual.Enunciado = UnirTexto(interpretacion.Actual.Enunciado, linea.Texto);
                }

                continue;
            }

            if (clasificada.Tipo == TipoLinea.Enunciado)
            {
                if (interpretacion.Actual is not null)
                    CerrarPregunta(interpretacion.Actual, preguntas, numeros, fiables, unirFragmentosOcr);

                var texto = linea.Numero is { Success: true }
                    ? linea.Numero.Groups["texto"].Value.Trim()
                    : linea.Texto.Trim();
                var numero = 0;

                if (linea.Numero is { Success: true })
                {
                    var numeroTexto = linea.Numero.Groups["numero"].Value;
                    numero = numeroTexto is "I" or "l" or "i"
                        ? 1
                        : int.TryParse(numeroTexto, out var valor) ? valor : 0;
                }
                interpretacion.Actual = new PreguntaEnCurso
                {
                    Enunciado = texto,
                    Opciones = [],
                    Numero = numero
                };
            }
        }
    }

    private static OpcionEnCurso ExtraerOpcion(Linea linea, int indice, bool unirFragmentosOcr)
    {
        var texto = linea.Texto.Trim();
        var letra = indice < 5 ? ((char)('a' + indice)).ToString() : string.Empty;
        var esCorrecta = false;
        var esIncorrecta = false;

        if (linea.Etiqueta is { Success: true } etiqueta)
        {
            letra = etiqueta.Groups["letra"].Value.ToLowerInvariant();
            texto = etiqueta.Groups["texto"].Value.Trim();
            esCorrecta = etiqueta.Groups["asterisco"].Success;
        }

        var prefijo = PatronPrefijoVeredicto.Match(texto);
        if (prefijo.Success)
        {
            esCorrecta = prefijo.Groups["veredicto"].Value.StartsWith("Correct", StringComparison.OrdinalIgnoreCase);
            esIncorrecta = !esCorrecta;
            texto = texto[prefijo.Length..].Trim();
        }

        texto = PatronRadio.Replace(texto, string.Empty);

        var marcaFinal = PatronVeredictoFinal.Match(texto);
        if (marcaFinal.Success)
        {
            esCorrecta = marcaFinal.Groups[1].Value.StartsWith("correct", StringComparison.OrdinalIgnoreCase);
            esIncorrecta = !esCorrecta;
            texto = texto[..marcaFinal.Index].Trim();
        }

        if (PatronMarcaRespuesta.IsMatch(texto))
        {
            esCorrecta = true;
            texto = PatronMarcaRespuesta.Replace(texto, string.Empty).Trim();
        }

        if (linea.Resaltada || linea.Negrita)
            esCorrecta = true;

        if (unirFragmentosOcr)
            texto = PatronRuidoOcr.Replace(texto, string.Empty).Trim();

        return new OpcionEnCurso
        {
            Letra = letra,
            Texto = texto,
            Negrita = linea.Negrita,
            Base = linea.Base,
            EsCorrecta = esCorrecta,
            EsIncorrecta = esIncorrecta
        };
    }

    private static void RecogerClave(Linea linea, Dictionary<int, string> claves)
    {
        if (!PatronPalabraClave.IsMatch(linea.Texto) && PatronClave.Matches(linea.Texto).Count < 2)
            return;

        foreach (Match match in PatronClave.Matches(linea.Texto))
        {
            if (int.TryParse(match.Groups["numero"].Value, out var numero))
                claves[numero] = match.Groups["letra"].Value.ToLowerInvariant();
        }
    }

    private static bool EsLineaDeClaves(string texto)
    {
        if (PatronClave.Matches(texto).Count >= 2)
            return true;

        var etiqueta = PatronPalabraClave.Match(texto);
        if (!etiqueta.Success)
            return false;

        var resto = texto[etiqueta.Length..].Trim();
        return resto.Length == 0
            || resto.StartsWith(':')
            || resto.StartsWith('-')
            || PatronClave.IsMatch(resto);
    }

    private static void AplicarClaves(
        List<Pregunta> preguntas,
        Dictionary<string, int> numeros,
        Dictionary<int, string> claves,
        HashSet<string> fiables)
    {
        for (var i = 0; i < preguntas.Count; i++)
        {
            var pregunta = preguntas[i];

            if (!numeros.TryGetValue(pregunta.Id, out var numero)
                || !claves.TryGetValue(numero, out var letra))
                continue;

            var opcion = pregunta.Opciones.FirstOrDefault(o =>
                string.Equals(o.Id, letra, StringComparison.OrdinalIgnoreCase));
            if (opcion is null)
                continue;

            preguntas[i] = pregunta with { ClaveRespuesta = opcion.Id };
            fiables.Add(pregunta.Id);
        }
    }

    private static void CerrarPregunta(
        PreguntaEnCurso actual,
        List<Pregunta> preguntas,
        Dictionary<string, int> numeros,
        HashSet<string> fiables,
        bool unirFragmentosOcr)
    {
        var enunciado = actual.Enunciado.Trim();
        if (unirFragmentosOcr)
        {
            var numeroCabecera = PatronNumeroCabeceraCorregida.Match(enunciado);
            if (numeroCabecera.Success)
            {
                var textoRestante = enunciado[numeroCabecera.Length..];
                var teniaMetadatos = false;

                while (true)
                {
                    var metadato = PatronMetadatoCabeceraCorregida.Match(textoRestante);
                    if (metadato.Success)
                    {
                        teniaMetadatos = true;
                        textoRestante = textoRestante[metadato.Length..];
                        continue;
                    }

                    var puntuacion = PatronPuntuacionCabeceraCorregida.Match(textoRestante);
                    if (puntuacion.Success)
                    {
                        textoRestante = textoRestante[puntuacion.Length..];
                        continue;
                    }

                    break;
                }

                if (teniaMetadatos && textoRestante.Length > 0)
                {
                    if (actual.Numero <= 0
                        && int.TryParse(numeroCabecera.Groups["numero"].Value, out var numeroDetectado))
                    {
                        actual.Numero = numeroDetectado;
                    }

                    enunciado = textoRestante.Trim();
                }
            }
        }

        var opciones = actual.Opciones
            .Where(o => !string.IsNullOrWhiteSpace(o.Texto))
            .GroupBy(o => o.Letra, StringComparer.OrdinalIgnoreCase)
            .Select(grupo => grupo.First())
            .ToList();

        if (enunciado.Length == 0 || opciones.Count < 2 || PatronArchivo.IsMatch(enunciado))
            return;

        var retroalimentacion = actual.TextoRetroalimentacion;
        if (!string.IsNullOrWhiteSpace(retroalimentacion))
        {
            var letra = Regex.Match(
                retroalimentacion,
                @"^\s*(?:(?:la\s+)?opci[oó]n\s+)?(?<letra>[a-e])(?:\b|[\)\].:\-])",
                RegexOptions.IgnoreCase);

            if (letra.Success)
            {
                var opcion = opciones.FirstOrDefault(o =>
                    string.Equals(o.Letra, letra.Groups["letra"].Value, StringComparison.OrdinalIgnoreCase));
                if (opcion is not null)
                {
                    foreach (var candidata in opciones)
                        candidata.EsCorrecta = ReferenceEquals(candidata, opcion);
                }
            }
            else
            {
                var normalizada = Normalizar(retroalimentacion);
                var coincidencias = opciones.Where(o =>
                    normalizada.Contains(Normalizar(o.Texto), StringComparison.Ordinal)
                    || Normalizar(o.Texto).Contains(normalizada, StringComparison.Ordinal)).ToList();
                if (coincidencias.Count == 1)
                {
                    foreach (var candidata in opciones)
                        candidata.EsCorrecta = ReferenceEquals(candidata, coincidencias[0]);
                }
            }
        }

        var correctas = opciones.Where(o => o.EsCorrecta).ToList();
        if (correctas.Count == 0)
        {
            var destacadas = opciones.Where(o => o.Negrita).ToList();
            if (destacadas.Count == 1)
                correctas = destacadas;
        }

        var correcta = correctas.Count == 1 ? correctas[0] : null;
        var id = $"p-{Guid.NewGuid():N}"[..12];
        var numero = actual.Numero > 0 ? actual.Numero : preguntas.Count + 1;
        var opcionesModelo = opciones
            .Select(o => new Opcion(o.Letra, o.Texto))
            .ToList();
        var clave = correcta?.Letra ?? opcionesModelo[0].Id;

        preguntas.Add(new Pregunta(id, enunciado, opcionesModelo, clave));
        numeros[id] = numero;
        if (correcta is not null)
            fiables.Add(id);
    }

    private static string UnirTexto(string texto, string continuacion)
    {
        var parte = continuacion.Trim();
        return parte.Length == 0 ? texto : texto.TrimEnd() + " " + parte;
    }

    private static string Normalizar(string texto)
    {
        var descompuesto = texto.Normalize(NormalizationForm.FormD);
        var caracteres = descompuesto
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .ToArray();
        return Regex.Replace(new string(caracteres).ToLowerInvariant(), @"\s+", " ").Trim();
    }
}