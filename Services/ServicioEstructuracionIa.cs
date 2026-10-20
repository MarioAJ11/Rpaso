using System.Text;
using System.Text.Json;
using LLama;
using LLama.Common;
using LLama.Native;
using LLama.Sampling;
using LLama.Transformers;
using Rpaso.Models;

namespace Rpaso.Services;

/// <summary>
/// Estructura el texto de un examen en preguntas mediante un modelo GGUF local (LLamaSharp).
/// Todo el proceso ocurre en la máquina: no hay ninguna llamada de red.
/// </summary>
public interface IServicioEstructuracionIa
{
    /// <summary>Ruta del modelo GGUF en uso, o null si no se ha cargado todavía.</summary>
    string? RutaModelo { get; }

    /// <summary>True si hay un modelo disponible en el disco.</summary>
    bool ModeloDisponible { get; }

    /// <summary>
    /// Convierte el texto del examen (pagina a pagina) en preguntas.
    /// </summary>
    Task<IReadOnlyList<Pregunta>> EstructurarAsync(IReadOnlyList<string> paginas, CancellationToken ct = default);
}

/// <inheritdoc cref="IServicioEstructuracionIa"/>
public sealed class ServicioEstructuracionIa : IServicioEstructuracionIa, IDisposable
{
    /// <summary>
    /// Gramática GBNF que obliga al modelo a emitir exactamente este JSON:
    /// <c>{"preguntas":[{"enunciado":"...","opciones":[{"id":"a","texto":"..."}],"claveRespuesta":"a"}]}</c>
    /// La gramática actúa token a token: el modelo no puede emitir JSON inválido ni texto extra,
    /// así que no hace falta reintentar ni validar con expresiones regulares.
    /// </summary>
    /// <remarks>
    /// OJO: los nombres de regla de GBNF solo admiten letras, dígitos y guiones.
    /// El guion bajo NO está permitido: "lista_opciones" se interpretaría como "lista" seguido
    /// de "_opciones" y el análisis de la gramática fallaría.
    /// </remarks>
    private const string GramáticaJson = """
        root    ::= "{" ws "\"preguntas\":" ws lista ws "}"
        lista   ::= "[" ws pregunta (ws "," ws pregunta)* ws "]"
        pregunta ::= "{" ws "\"enunciado\":" ws str ws "," ws "\"opciones\":" ws listaOpciones ws "," ws "\"claveRespuesta\":" ws str ws "}"
        listaOpciones ::= "[" ws opcion (ws "," ws opcion)* ws "]"
        opcion  ::= "{" ws "\"id\":" ws str ws "," ws "\"texto\":" ws str ws "}"
        str     ::= "\"" char* "\""
        char    ::= [^"\\] | "\\" (["\\/bfnrt] | "u" [0-9a-fA-F]{4})
        ws      ::= [ \t\n\r]*
        """;

    private const string InstruccionesSistema = """
        Eres un extractor de examenes tipo test. Conviertes el texto de un examen en un JSON con TODAS sus preguntas.

        Formato de salida, exactamente con estos nombres de campo:
        {"preguntas":[{"enunciado":"...","opciones":[{"id":"a","texto":"..."}],"claveRespuesta":"a"}]}

        Reglas:
        - El texto contiene N preguntas. Debes extraerlas TODAS: una entrada en "preguntas" por cada pregunta.
          No te detengas hasta haber extraido la ultima pregunta del texto.
        - "enunciado" es el texto de la pregunta, SIN el numero inicial y SIN las letras de las opciones.
        - "id" de cada opcion es SU LETRA (a, b, c, d) y "texto" es el contenido SIN la letra delante.
          Ejemplo: el texto "b) 46" se extrae como id "b" y texto "46".
        - Copia el texto EXACTAMENTE como aparece. No lo corrijas, no lo traduzcas, no lo reescribas:
          si el examen pone "Fotosintesis", escribe "Fotosintesis".
        - "claveRespuesta" es la LETRA de la opcion correcta (a, b, c, d). Si el examen da la clave como
          "1-b, 2-b", extrae solo la letra de la opcion: "b".
        - Si el examen no incluye la clave, usa la letra de la primera opcion.
        - Ignora los textos que no sean preguntas con opciones.

        EJEMPLO
        Entrada:
        1. Cual es la capital de Francia?
           a) Berlin
           b) Paris
           c) Roma
        2. Cuantos lados tiene un triangulo?
           a) 2
           b) 3
           c) 4
        Respuestas: 1-b, 2-b

        Salida:
        {"preguntas":[{"enunciado":"Cual es la capital de Francia?","opciones":[{"id":"a","texto":"Berlin"},{"id":"b","texto":"Paris"},{"id":"c","texto":"Roma"}],"claveRespuesta":"b"},{"enunciado":"Cuantos lados tiene un triangulo?","opciones":[{"id":"a","texto":"2"},{"id":"b","texto":"3"},{"id":"c","texto":"4"}],"claveRespuesta":"b"}]}
        """;

    private const int TamanoContexto = 4096;
    private const int TokensMaximosSalida = 1536;

    /// <summary>
    /// Presupuesto de caracteres por fragmento. El contexto del modelo es de 4096 tokens y la
    /// salida puede ocupar 1536, así que solo quedan unos 2500 tokens para el prompt:
    /// en español eso son del orden de 6000 caracteres.
    /// </summary>
    private const int CaracteresPorFragmento = 5500;

    private static readonly JsonSerializerOptions OpcionesJson = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly SemaphoreSlim _semforo = new(1, 1);
    private LLamaWeights? _pesos;
    private StatelessExecutor? _ejecutor;
    private bool _descartado;

    private static bool _nativasConfiguradas;

    public string? RutaModelo { get; private set; }

    public bool ModeloDisponible => BuscarModelo() is not null;

    /// <summary>
    /// LLamaSharp localiza sus binarios nativos con rutas RELATIVAS al directorio de trabajo
    /// (runtimes/win-x64/native/avx2/llama.dll). Si la aplicación se lanza con otro directorio
    /// de trabajo -por ejemplo con "dotnet run", que arranca en la carpeta del proyecto- la
    /// ruta relativa no resuelve y la carga nativa falla. Registrando la carpeta base de la
    /// aplicación como directorio de búsqueda, la resolución es correcta siempre.
    /// </summary>
    private static void ConfigurarNativas()
    {
        if (_nativasConfiguradas)
            return;

        _nativasConfiguradas = true;

        try
        {
            NativeLibraryConfig.All.WithSearchDirectory(AppContext.BaseDirectory);
            NativeLibraryConfig.All.WithLogCallback((_, _) => { });
        }
        catch (InvalidOperationException)
        {
            // La biblioteca nativa ya se cargó: no se puede reconfigurar y no hace falta.
        }
    }

    /// <summary>
    /// Busca el modelo GGUF en este orden:
    /// 1. Variable de entorno RPASO_MODELO (ruta completa a un .gguf).
    /// 2. %APPDATA%\Rpaso\Modelos (modelos que gestiona el usuario).
    /// 3. Carpeta Modelos junto al ejecutable (distribución con el modelo incluido).
    /// 4. Carpeta Assets\Modelos junto al ejecutable.
    /// 5. Carpeta Assets\Modelos de la raíz del proyecto, para desarrollo.
    /// </summary>
    public static string? BuscarModelo()
    {
        var porVariable = Environment.GetEnvironmentVariable("RPASO_MODELO");
        if (!string.IsNullOrWhiteSpace(porVariable) && File.Exists(porVariable))
            return porVariable;

        var directorioBase = AppContext.BaseDirectory;

        var candidatos = new List<string>
        {
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Rpaso", "Modelos"),
            Path.Combine(directorioBase, "Modelos"),
            Path.Combine(directorioBase, "Assets", "Modelos")
        };

        // En desarrollo el ejecutable vive en bin\<config>\net10.0, así que se sube
        // hasta la raíz del proyecto para encontrar Assets\Modelos.
        var directorioProyecto = BuscarRaizProyecto(directorioBase);
        if (directorioProyecto is not null)
            candidatos.Add(Path.Combine(directorioProyecto, "Assets", "Modelos"));

        foreach (var directorio in candidatos)
        {
            if (!Directory.Exists(directorio))
                continue;

            var modelo = Directory.EnumerateFiles(directorio, "*.gguf")
                .OrderByDescending(r => new FileInfo(r).Length)
                .FirstOrDefault();

            if (modelo is not null)
                return modelo;
        }

        return null;
    }

    /// <summary>
    /// Sube desde el directorio del ejecutable buscando la carpeta de modelos del proyecto.
    /// </summary>
    private static string? BuscarRaizProyecto(string directorioBase)
    {
        var actual = new DirectoryInfo(directorioBase);

        for (var nivel = 0; nivel < 6 && actual is not null; nivel++)
        {
            if (Directory.Exists(Path.Combine(actual.FullName, "Assets", "Modelos")))
                return actual.FullName;

            actual = actual.Parent;
        }

        return null;
    }

    public async Task<IReadOnlyList<Pregunta>> EstructurarAsync(
        IReadOnlyList<string> paginas,
        CancellationToken ct = default)
    {
        if (paginas.Count == 0)
            return [];

        await EnsureModeloCargadoAsync(ct);

        var preguntas = new List<Pregunta>();
        var vistas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var fragmento in Trocear(paginas))
        {
            ct.ThrowIfCancellationRequested();

            var respuesta = await GenerarJsonAsync(fragmento, ct);
            if (respuesta is null)
                continue;

            foreach (var extraida in respuesta.Preguntas)
            {
                var enunciado = extraida.Enunciado?.Trim();
                if (string.IsNullOrWhiteSpace(enunciado))
                    continue;

                var (opciones, clave) = NormalizarOpciones(extraida);
                if (opciones.Count < 2)
                    continue;

                // Deduplicación: una misma pregunta partida entre fragmentos no debe repetirse.
                if (!vistas.Add(enunciado))
                    continue;

                preguntas.Add(new Pregunta(
                    $"q-{preguntas.Count + 1}",
                    enunciado,
                    opciones,
                    clave));
            }
        }

        return preguntas;
    }

    /// <summary>
    /// Normaliza las opciones y la clave de respuesta.
    /// Los modelos pequeños suelen meter el identificador de la opción dentro del texto
    /// ("a) 46" como texto con id "1") y copiar la clave tal cual aparece en el examen
    /// ("1-b" en lugar de "b"). Aquí se corrige de forma determinista.
    /// </summary>
    private static (List<Opcion> Opciones, string Clave) NormalizarOpciones(PreguntaExtraida extraida)
    {
        var opciones = new List<Opcion>();

        foreach (var opcion in extraida.Opciones ?? [])
        {
            var texto = (opcion.Texto ?? "").Trim();
            var id = (opcion.Id ?? "").Trim();

            // Si el texto empieza por "a)" / "a." / "a:" la etiqueta real está en el texto.
            if (texto.Length >= 2 && (texto[1] == ')' || texto[1] == '.' || texto[1] == ':') && char.IsLetterOrDigit(texto[0]))
            {
                id = texto[..1];
                texto = texto[2..].Trim();
            }

            if (string.IsNullOrWhiteSpace(texto))
                continue;

            opciones.Add(new Opcion(string.IsNullOrWhiteSpace(id) ? "a" : id, texto));
        }

        var clave = (extraida.ClaveRespuesta ?? "").Trim();

        // La clave puede venir como "1-b" (pregunta-respuesta) o "b)": se queda con la parte útil.
        var candidatos = new List<string> { clave };

        var guion = clave.LastIndexOfAny(['-', ':', '=', ',']);
        if (guion >= 0 && guion < clave.Length - 1)
            candidatos.Add(clave[(guion + 1)..].Trim());

        if (clave.Length >= 2 && (clave[1] == ')' || clave[1] == '.' || clave[1] == ':'))
            candidatos.Add(clave[..1]);

        var claveResuelta = candidatos
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .FirstOrDefault(c => opciones.Any(o => string.Equals(o.Id, c, StringComparison.OrdinalIgnoreCase)));

        return (opciones, claveResuelta ?? opciones.FirstOrDefault()?.Id ?? "a");
    }

    /// <summary>
    /// Agrupa las páginas en fragmentos que caben en el contexto del modelo sin partir
    /// una pregunta por la mitad salvo que no haya más remedio.
    /// </summary>
    private static IEnumerable<string> Trocear(IReadOnlyList<string> paginas)
    {
        var actual = new StringBuilder();

        foreach (var pagina in paginas)
        {
            if (actual.Length > 0 && actual.Length + pagina.Length > CaracteresPorFragmento)
            {
                yield return actual.ToString();
                actual.Clear();
            }

            actual.AppendLine(pagina);
        }

        if (actual.Length > 0)
            yield return actual.ToString();
    }

    private async Task EnsureModeloCargadoAsync(CancellationToken ct)
    {
        if (_ejecutor is not null)
            return;

        await _semforo.WaitAsync(ct);
        try
        {
            if (_ejecutor is not null)
                return;

            var ruta = BuscarModelo()
                ?? throw new FileNotFoundException(
                    "No se encontró ningún modelo GGUF. Coloca un archivo .gguf en la carpeta " +
                    "Modelos junto al ejecutable, o define la variable de entorno RPASO_MODELO.");

            // Debe hacerse antes de construir ModelParams: su constructor ya toca la nativa.
            ConfigurarNativas();

            var parametros = new ModelParams(ruta)
            {
                ContextSize = TamanoContexto,
                GpuLayerCount = 0,
                Threads = Environment.ProcessorCount
            };

            // Cargar el modelo es una operación pesada: nunca en el hilo de la UI.
            var pesos = await Task.Run(() => LLamaWeights.LoadFromFile(parametros), ct);

            _pesos = pesos;
            _ejecutor = new StatelessExecutor(pesos, parametros, null)
            {
                // La plantilla de chat la componemos nosotros con LLamaTemplate:
                // si el ejecutor la aplicase otra vez, el prompt quedaría duplicado.
                ApplyTemplate = false
            };
            RutaModelo = ruta;
        }
        finally
        {
            _semforo.Release();
        }
    }

    private async Task<RespuestaExtraida?> GenerarJsonAsync(string fragmento, CancellationToken ct)
    {
        var pesos = _pesos!;
        var ejecutor = _ejecutor!;

        var plantilla = new LLamaTemplate(pesos, strict: false);
        plantilla.Add("system", InstruccionesSistema);
        plantilla.Add("user",
            $"Extrae las preguntas de este examen:\n\n<examen>\n{fragmento}\n</examen>");

        // LLamaTemplate.Apply() devuelve un ReadOnlySpan<byte> (UTF-8) cuyo ToString() NO es el
        // contenido. ToModelPrompt es la API correcta para obtener el prompt como string.
        var prompt = PromptTemplateTransformer.ToModelPrompt(plantilla);

        // Muestreo codicioso: salida determinista, sin randomness que gaste tokens.
        var muestreo = new GreedySamplingPipeline
        {
            Grammar = new Grammar(GramáticaJson, "root")
        };

        var parametrosInferencia = new InferenceParams
        {
            MaxTokens = TokensMaximosSalida,
            SamplingPipeline = muestreo,
            AntiPrompts = []
        };

        var construccion = new StringBuilder();

        // El contexto de llama.cpp no es seguro ante uso concurrente: se serializa el acceso.
        await _semforo.WaitAsync(ct);
        try
        {
            await foreach (var fragmentoSalida in ejecutor.InferAsync(prompt, parametrosInferencia, ct))
            {
                construccion.Append(fragmentoSalida);

                // Parada temprana: en cuanto el JSON acumulado sea válido, no hay nada más que
                // generar. Sin esto el modelo sigue emitiendo preguntas hasta MaxTokens.
                if (construccion.Length > 0 && construccion[^1] == '}' && EsJsonCompleto(construccion.ToString()))
                    break;
            }
        }
        finally
        {
            _semforo.Release();
        }

        return InterpretarJson(construccion.ToString(), fragmento);
    }

    private static bool EsJsonCompleto(string json)
    {
        try
        {
            _ = JsonSerializer.Deserialize<RespuestaExtraida>(json, OpcionesJson);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Interpreta el JSON emitido por el modelo. Si la generación se cortó por el límite de
    /// tokens, se intenta recuperar el último objeto completo en lugar de perder todo el fragmento.
    /// </summary>
    private static RespuestaExtraida? InterpretarJson(string json, string fragmento)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        if (Environment.GetEnvironmentVariable("RPASO_DEBUG_JSON") == "1")
        {
            Console.WriteLine("===== PROMPT =====");
            Console.WriteLine(fragmento);
            Console.WriteLine("===== JSON CRUDO =====");
            Console.WriteLine(json);
            Console.WriteLine("===== FIN =====");
        }

        var candidatas = new List<string> { json };

        var ultimoCierre = json.LastIndexOf('}');
        if (ultimoCierre > 0 && ultimoCierre < json.Length - 1)
            candidatas.Add(json[..(ultimoCierre + 1)]);

        foreach (var candidata in candidatas)
        {
            try
            {
                return JsonSerializer.Deserialize<RespuestaExtraida>(candidata, OpcionesJson);
            }
            catch (JsonException)
            {
                // Se prueba la siguiente variante (por ejemplo, recortar el último objeto inacabado).
            }
        }

        return null;
    }

    public void Dispose()
    {
        if (_descartado)
            return;

        _descartado = true;
        // StatelessExecutor no es IDisposable: el contexto es lo que hay que liberar.
        _ejecutor?.Context?.Dispose();
        _pesos?.Dispose();
        _semforo.Dispose();
    }
}
