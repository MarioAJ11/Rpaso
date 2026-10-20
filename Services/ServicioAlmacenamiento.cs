using System.Text.Encodings.Web;
using System.Text.Json;
using Rpaso.Models;

namespace Rpaso.Services;

/// <summary>
/// Persistencia en ficheros JSON bajo %APPDATA%\Rpaso.
/// </summary>
public sealed class ServicioAlmacenamiento : IServicioAlmacenamiento
{
    private const string FicheroTests = "tests.json";
    private const string FicheroProgreso = "progreso.json";

    private static readonly JsonSerializerOptions OpcionesJson = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,

        // Sin esto, System.Text.Json escapa los acentos como \u00E9 y el fichero
        // queda ilegible. Es seguro porque el JSON solo lo lee esta misma aplicación,
        // nunca se inyecta en una página web.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly string _directorio;

    /// <param name="directorio">
    /// Permite inyectar una ruta alternativa. En los tests se usa una carpeta temporal;
    /// si es null se usa %APPDATA%\Rpaso.
    /// </param>
    public ServicioAlmacenamiento(string? directorio = null)
    {
        _directorio = directorio ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Rpaso");

        Directory.CreateDirectory(_directorio);
    }

    public async Task<IReadOnlyList<Test>> CargarTestsAsync(CancellationToken ct = default)
        => await LeerAsync<List<Test>>(FicheroTests, ct) ?? [];

    public async Task GuardarTestAsync(Test test, CancellationToken ct = default)
    {
        var tests = (await CargarTestsAsync(ct)).ToList();
        var indice = tests.FindIndex(t => t.Id == test.Id);

        if (indice >= 0)
            tests[indice] = test;
        else
            tests.Add(test);

        await EscribirAsync(FicheroTests, tests, ct);
    }

    public async Task<IReadOnlyList<ProgresoPregunta>> CargarProgresoAsync(CancellationToken ct = default)
        => await LeerAsync<List<ProgresoPregunta>>(FicheroProgreso, ct) ?? [];

    public async Task GuardarProgresoAsync(ProgresoPregunta progreso, CancellationToken ct = default)
    {
        var lista = (await CargarProgresoAsync(ct)).ToList();
        var indice = lista.FindIndex(p => p.PreguntaId == progreso.PreguntaId);

        if (indice >= 0)
            lista[indice] = progreso;
        else
            lista.Add(progreso);

        await EscribirAsync(FicheroProgreso, lista, ct);
    }

    private async Task<T?> LeerAsync<T>(string nombreFichero, CancellationToken ct)
    {
        var ruta = Path.Combine(_directorio, nombreFichero);

        if (!File.Exists(ruta))
            return default;

        try
        {
            var json = await File.ReadAllTextAsync(ruta, ct);
            return JsonSerializer.Deserialize<T>(json, OpcionesJson);
        }
        catch (JsonException)
        {
            // Fichero corrupto o de una versión anterior: se descarta y se empieza de cero
            // en lugar de impedir que la aplicación arranque.
            return default;
        }
    }

    private async Task EscribirAsync<T>(string nombreFichero, T contenido, CancellationToken ct)
    {
        var ruta = Path.Combine(_directorio, nombreFichero);
        var rutaTemporal = ruta + ".tmp";

        var json = JsonSerializer.Serialize(contenido, OpcionesJson);
        await File.WriteAllTextAsync(rutaTemporal, json, ct);

        // Move con overwrite es atómico en Windows: o el fichero antiguo está o el nuevo.
        // Así un cierre inesperado a mitad de escritura no deja un JSON corrupto.
        File.Move(rutaTemporal, ruta, overwrite: true);
    }
}
