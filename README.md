# Repaso

Aplicación de escritorio para **convertir PDFs de exámenes tipo test en tests interactivos** y practicarlos.
Todo el procesamiento ocurre **en local**: el modelo de lenguaje y la extracción de texto se ejecutan en tu
propio equipo. No hay cuentas, ni claves de API, ni conexión a internet.

- **Frontend:** Avalonia UI 12 (XAML)
- **Lógica:** C# · .NET 10 · MVVM con `CommunityToolkit.Mvvm`
- **Extracción de texto:** `PdfPig` (paquete NuGet `PdfPig`, namespace `UglyToad.PdfPig`)
- **Estructuración con IA:** `LLamaSharp` + `llama.cpp` con un modelo **GGUF** local

---

## Puesta en marcha

### 1. Requisitos

- **.NET 10 SDK** (o superior).
- Un **modelo GGUF** descargado (ver siguiente punto).

### 2. Descargar el modelo

La aplicación busca el modelo en este orden:

1. Variable de entorno `RPASO_MODELO` (ruta completa a un `.gguf`).
2. `%APPDATA%\Rpaso\Modelos\*.gguf`.
3. `Modelos\*.gguf` junto al ejecutable.
4. `Assets\Modelos\*.gguf` de la raíz del proyecto (desarrollo).

Modelos recomendados:

| Modelo | Tamaño | Calidad |
| --- | --- | --- |
| [`Qwen2.5-1.5B-Instruct-GGUF`](https://huggingface.co/Qwen/Qwen2.5-1.5B-Instruct-GGUF) (`q4_k_m`) | 1,06 GB | ✅ **Recomendado** |
| [`Qwen2.5-0.5B-Instruct-GGUF`](https://huggingface.co/Qwen/Qwen2.5-0.5B-Instruct-GGUF) (`q4_k_m`) | 468 MB | Más rápido, menos exacto con las claves de respuesta |

Descarga directa:

```powershell
mkdir -Force Assets\Modelos
Invoke-WebRequest "https://huggingface.co/Qwen/Qwen2.5-1.5B-Instruct-GGUF/resolve/main/qwen2.5-1.5b-instruct-q4_k_m.gguf?download=true" -OutFile "Assets\Modelos\qwen2.5-1.5b-instruct-q4_k_m.gguf"
```

> Si hay varios modelos en la carpeta, la aplicación usa **el más grande**.

### 3. Ejecutar

```powershell
dotnet run
```

### 4. Verificar el pipeline por consola (sin interfaz)

```powershell
dotnet run -- --verificar "C:\ruta\a\tu\examen.pdf"
```

Imprime las preguntas extraídas y la clave de cada una. Útil para depurar el modelo.

Y si lo que falla es la **extracción** (no el modelo), este modo te enseña el texto tal cual sale del PDF:

```powershell
dotnet run -- --texto "C:\ruta\a\tu\examen.pdf"
```

> Si dice "Páginas con texto: 0", el PDF es un **escaneo sin capa de texto**: la app no puede leerlo
> (haría falta OCR).

### 5. Publicar (ejecutable autocontenido)

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -o publicacion
```

El resultado incluye `Rpaso.exe`, las bibliotecas nativas de `llama.cpp` y los modelos en `Modelos/`.
El usuario final solo hace **doble clic** en `Rpaso.exe`: no necesita instalar .NET ni nada más.

> Para distribución, Borra el modelo que no Uses de `Assets\Modelos` antes de publicar: cada modelo
> añade su tamaño al ejecutable.

---

## Cómo funciona la importación

```
PDF  ──►  PdfPig (texto por página)  ──►  troceado por contexto  ──►  modelo GGUF local
                                                                      │
                          Modelos/ViewModels/Views  ◄──  JSON estricto ◄┘
```

1. **PdfPig** extrae el texto de cada página conservando los saltos de línea (son la frontera entre
   enunciado y opciones).
2. El texto se **trocea** en fragmentos que caben en el contexto del modelo, sin partir preguntas.
3. Cada fragmento se envía al modelo local con una **gramática GBNF** que obliga a emitir exactamente
   este JSON, token a token:

   ```json
   {"preguntas":[{"enunciado":"...","opciones":[{"id":"a","texto":"..."}],"claveRespuesta":"a"}]}
   ```

   Gracias a la gramática el modelo **no puede** emitir JSON inválido: no hace falta reintentar ni
   validar con expresiones regulares.
4. La salida se deserializa a `Models/Pregunta` y se normaliza (el modelo a veces mete la letra de la
   opción dentro del texto o copia la clave tal cual aparece: `1-b` → `b`).
5. El test se guarda en `%APPDATA%\Rpaso\tests.json`.

El progreso se guarda en `%APPDATA%\Rpaso\progreso.json` y alimenta las preguntas **falladas**
("por reforzar") que puedes repasar en bloque.

---

## Estructura del proyecto

```
Rpaso/
├── Models/                 Modelo de dominio (records inmutables, sin dependencias de UI)
│   ├── Carpeta.cs          Carpeta temática
│   ├── ColorCarpeta.cs     Paleta de colores
│   ├── Opcion.cs
│   ├── Pregunta.cs
│   ├── ProgresoPregunta.cs Progreso del usuario sobre cada pregunta
│   └── Test.cs             Examen importado
├── Services/               Infraestructura
│   ├── IServicioAlmacenamiento.cs   Persistencia JSON
│   ├── IServicioExtraccionPdf.cs   Lectura del PDF
│   ├── IServicioImportacion.cs     Caso de uso: PDF → test guardado
│   ├── IServicioEstructuracionIa.cs
│   ├── ServicioAlmacenamiento.cs
│   ├── ServicioExtraccionPdf.cs
│   ├── ServicioEstructuracionIa.cs  LLamaSharp + gramática GBNF
│   └── RespuestaExtraida.cs         DTO de la respuesta del modelo
├── ViewModels/             Presentación y estado (CommunityToolkit.Mvvm)
├── Views/                  XAML
│   ├── MainWindow.axaml    Shell: barra lateral + cabecera + contenido + modal
│   ├── HomeView.axaml      Inicio: métricas y carpetas
│   ├── FolderView.axaml    Carpetas: PDFs y acciones de práctica
│   └── QuizView.axaml      Modo examen
├── Assets/Estilos/
│   ├── Tema.axaml          Paleta (resources)
│   └── Estilos.axaml       Estilos de controles (styles)
├── App.axaml               Registro de recursos, estilos y ViewLocator
├── ViewLocator.cs          ViewModel → View por convención de nombre
└── ModoVerificacion.cs     Modo consola `--verificar`
```

### Navegación

`MainViewModel.VistaActual` es un `object?`. Se enlaza al `ContentControl` del `MainWindow` y el
**`ViewLocator`** convierte el ViewModel en su vista por convención:

```
Rpaso.ViewModels.HomeViewModel  →  Rpaso.Views.HomeView
```

Por eso las vistas **deben** llamarse `XxxView` (no `XxxWindow`): el `ViewLocator` sustituye
`"ViewModel"` por `"View"` en el nombre del tipo.

---

## Casos reales: qué esperar

El comportamiento depende mucho del PDF de origen. Estos son los casos que se han comprobado con
exámenes reales:

| Tipo de PDF | Qué ocurre |
| --- | --- |
| Texto con opciones `a) b) c)` y clave de respuestas | ✅ Funciona perfecto: enunciados, opciones y claves correctas |
| Texto con opciones **sin letra** (una línea por opción) | ⚠️ Funciona: el modelo asigna las letras `a, b, c...` por orden |
| Examen de tipo **"señalale la respuesta incorrecta"** | ⚠️ La clave guarda la opción **incorrecta**, no la correcta |
| PDF **sin clave de respuestas** | ⚠️ El modelo debe adivinar la clave: se usa la primera opción |
| PDF **escaneado** (sin capa de texto) | ❌ "Páginas con texto: 0". Necesita OCR |
| PDF con **anuncios y avisos legales** | ⚠️ Pueden aparecer como preguntas espurias |

### Ruido del documento

Los PDF de descarga (wuolah, campus virtuales…) traen **anuncios, avisos de derechos de autor,
portadas y marcas de agua**. El prompt le dice al modelo que los ignore, pero un modelo de 1,5B
a veces se cuelga con la portada y se inventa alguna pregunta a partir de ella.

Si te pasa, la solución más simple es **un modelo más grande** (Qwen2.5-3B, ~2 GB) o **limpiar el
PDF antes de importarlo**.

---

## Notas técnicas (trampas encontradas)

- **`PdfPig`, no `UglyToad.PdfPig`.** El paquete de NuGet cambió de ID; el *namespace* sigue siendo
  `UglyToad.PdfPig`. Las versiones estables (`0.1.16`) están en `PdfPig`; el ID antiguo ya no las publica.
- **`.NET 10` es necesario.** La build `netstandard2.0` de LLamaSharp 0.30.0 no incluye
  `NativeLibraryConfig.WithSearchDirectory`, que hace falta para localizar los binarios nativos de
  llama.cpp cuando el directorio de trabajo no es la carpeta del ejecutable.
- **Los nombres de regla GBNF no admiten guion bajo.** `lista_opciones` se interpreta como `lista`
  seguido de `_opciones` y el análisis de la gramática falla. Usa `listaOpciones` o `lista-opciones`.
- **`LLamaTemplate.Apply()` devuelve un `ReadOnlySpan<byte>`**, cuyo `ToString()` es
  `"System.ReadOnlySpan<Byte>[1234]"`, no el contenido. Usa
  `PromptTemplateTransformer.ToModelPrompt(plantilla)`.
- **El modelo no se copia en cada compilación** (son cientos de MB y bloquearía el fichero mientras se
  descarga). Se copia al publicar mediante el objetivo `CopiarModelosAlPublicar`.
- **Windows Defender Application Control (WDAC)** de algunas empresas bloquea binarios gestionados sin
  firmar. Si el ejecutable no arranca, mira el log
  `Microsoft-Windows-CodeIntegrity/Operational`.

### Depuración

`RPASO_DEBUG_JSON=1` imprime el prompt y el JSON crudo que genera el modelo.

`RPASO_TEST_VISTA=carpeta|quiz` arranca directamente en una vista concreta (solo para depuración).
