# Repaso

Aplicación de escritorio para **convertir PDFs de exámenes tipo test en tests interactivos** y practicarlos.

Todo funciona **en local y sin IA**: el análisis del PDF se hace con un **parser geométrico** que trabaja con
las coordenadas del texto. No hay modelos, ni claves de API, ni conexión a internet, ni ficheros de más de 100 MB.

- **Frontend:** Avalonia UI 12 (XAML)
- **Lógica:** C# · .NET 10 · MVVM con `CommunityToolkit.Mvvm`
- **Extracción:** `PdfPig` (paquete NuGet `PdfPig`, namespace `UglyToad.PdfPig`)

---

## Puesta en marcha

```powershell
.\herramientas\compilar.ps1      # compila y firma (ver la nota de Windows 11)
dotnet run
```

Sin más dependencias: no hay modelos que descargar ni que instalar.

### ⚠️ Firmado de binarios en Windows 11 (obligatorio)

Windows 11 distribuye la política de Code Integrity `VerifiedAndReputableDesktopFlightSupplemental`,
que **exige que el ejecutable esté firmado** (o tenga reputación en la nube de Microsoft). Un binario
recién compilado no la tiene, y Windows lo bloquea con:

```
Una directiva de Control de aplicaciones bloqueó este archivo. (0x800711C7)
```

Es lo que hace que la app funcione un día y al siguiente no: la política se refresca con Windows
Update y el binario cambia de hash en cada compilación.

`herramientas\firmar.ps1` crea un certificado autofirmado en el almacén del usuario (suficiente para
desarrollo) y firma `Rpaso.exe` y `Rpaso.dll`.

> **Para distribuir al público** hace falta un **certificado de firma de código comercial**: el
> autofirmado solo vale en el equipo donde se creó. En un entorno corporativo debe emitirlo la CA
> de la empresa.

### Verificar el pipeline por consola

```powershell
# Resultado completo: preguntas, opciones y claves
dotnet run -- --verificar "C:\ruta\a\tu\examen.pdf"

# Importar varios y ver el informe de fallos
dotnet run -- --importar "a.pdf" "b.pdf" "c.pdf"

# Texto crudo que sale del PDF (para ver si tiene capa de texto)
dotnet run -- --texto "C:\ruta\a\tu\examen.pdf"

# Geometría: cada palabra con su X y su Y
dotnet run -- --geometria "C:\ruta\a\tu\examen.pdf" 2

# Cómo clasifica el parser cada línea (enunciado / opción / continuación / encabezado)
dotnet run -- --analizar "C:\ruta\a\tu\examen.pdf" 2
```

> Si `--texto` dice "Páginas con texto: 0", el PDF es un **escaneo sin capa de texto**:
> la app no puede leerlo (haría falta OCR).

### Publicar (ejecutable autocontenido)

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -o publicacion
powershell -ExecutionPolicy Bypass -File .\herramientas\firmar.ps1 -Ruta .\publicacion
```

El usuario final solo hace **doble clic** en `Rpaso.exe`: no necesita instalar .NET ni nada más.
**Firmarlo es obligatorio** en equipos con Smart App Control (ver la nota de arriba).

---

## Cómo funciona el parser geométrico

El problema: los PDFs de descarga (wuolah, campus virtuales) traen **publicidad, avisos legales, marcas de
agua y portadas** mezclados con las preguntas. Un parser de texto plano se los come.

La solución: **trabajar con las coordenadas**, no con la cadena de texto.

```
PDF
 │
 ├─ 1) Descartar los márgenes por coordenada Y
 │     La Y de PDF crece hacia arriba: el pie de página son las Y pequeñas.
 │     Se tira todo lo que esté por debajo del 5,5% inferior y por encima del 4,5% superior.
 │
 ├─ 2) Reconstruir las líneas agrupando por línea base
 │     Cada palabra tiene una Y base. Las palabras con la misma Y base son la misma línea.
 │     Se unen respetando los huecos horizontales (una columna a la izquierda y otra a la
 │     derecha NO son la misma frase).
 │
 ├─ 3) Clasificar cada línea
 │     • "a)", "b." …            → opción (etiqueta explícita)
 │     • sangrada respecto a X   → opción sin letra (los exams de Kansei no las traen)
 │     • termina en . ? ! :      → la frase está completa, lo siguiente es otra cosa
 │     • en la misma columna y con salto vertical pequeño → continuación de la anterior
 │     • resto                   → enunciado
 │
 └─ 4) Ensamblar preguntas y aplicar la clave de respuestas
```

### Las señales geométricas que se usan

| Señal | Para qué |
|---|---|
| `BoundingBox.Bottom` | Línea base: agrupa palabras en la misma línea |
| `BoundingBox.Top` / `Height` | Descartar márgenes y calcular tolerancias |
| `BoundingBox.Left` | Detectar la sangría de las opciones y alinear continuaciones |
| Salto vertical entre líneas | Distinguir "continuación" de "pregunta nueva" |
| `Letter.FontDetails.IsBold` | Detectar la opción correcta (en estos tests va en negrita) |

### Cómo se averigua la respuesta correcta

Por orden de fiabilidad:

1. **Marca explícita del PDF**:
   - `Correcta:` delante del texto de la opción → esa es la buena.
   - `Respuesta correcta` en el margen derecho → esa es la buena.
   - `Incorrecta:` → esa es la mala (informativo, no se usa como clave).
2. **Clave numérica** en el PDF (`Respuestas: 1-b, 2-c`).
3. **Opción en negrita**: si hay exactamente una, esa es la correcta.
4. Si no hay ninguna, se usa la primera opción.

> La negrita se lee de `FontDetails.IsBold`, **no** del nombre de la fuente: el prefijo de
> subconjunto (`DAAAAA+Times`) se asigna por orden de aparición y no dice nada.
>
> No todos los exams marcan la respuesta en negrita. De los PDFs revisados, 3 sí lo hacen
> (MEGATEST, TEST-TEORIA, Tests-Respuestas) y 4 no (Tema-2, los de Kansei, Test-1-parcial):
> en esos la clave cae en la primera opción hasta que se corrija a mano.

### Formatos de PDF soportados

| Formato | Cómo se detecta |
| --- | --- |
| Opciones con letra (`a)`, `b.` …) | Etiqueta al inicio de la línea |
| Opciones sin letra (una línea por opción) | Sangría en X respecto a la columna del enunciado |
| `• A) texto` con viñeta | La viñeta se admite delante de la etiqueta |
| Campus virtual (`Correcta:` / `Incorrecta:` + `Respuesta correcta` a la derecha) | Las marcas del propio PDF |
| Opciones partidas en varias líneas | Se unen por rango de X y salto vertical |
| Enunciado partido en dos líneas | Se unen salvo que la anterior termine en puntuación |
| `Pregunta N` como encabezado | Se ignora: no es una pregunta |
| Nota en el margen derecho | Se parte la línea en los huecos horizontales grandes |

### Rendimiento real medido

| PDF | Preguntas | Tiempo |
| --- | --- | --- |
| wuolah-free-Tests-Respuestas.pdf (66 pág.) | 388 | 1,8 s |
| wuolah-free-RECOPILACION-TEST-PROCESOS.pdf | 287 | 1,9 s |
| wuolah-free-MEGATEST-PROCESOS-1P.pdf | 190 | 1,4 s |
| wuolah-free-TEST-PRIMER-PARCIAL-KANSEI.pdf | 173 | 1,4 s |
| wuolah-free-Test-Parcial-1-Kansei.pdf (sin letras en las opciones) | 151 | 0,6 s |
| wuolah-free-TEST-TEORIA.pdf | 50 | 1,3 s |
| wuolah-free-Test-1-parcial.pdf | 22 | 1,1 s |
| wuolah-free-Tema-2.pdf | 12 | 1,2 s |

---

## Estructura del proyecto

```
Rpaso/
├── Models/                     Modelo de dominio (records inmutables, sin UI)
│   ├── Carpeta.cs              Carpeta temática
│   ├── ColorCarpeta.cs         Paleta de colores
│   ├── Opcion.cs
│   ├── Pregunta.cs
│   ├── ProgresoPregunta.cs     Progreso del usuario sobre cada pregunta
│   └── Test.cs                 Examen importado
├── Services/
│   ├── IServicioAlmacenamiento.cs   Persistencia JSON
│   ├── IServicioExtraccionPdf.cs   Lectura del PDF con PdfPig
│   ├── IServicioImportacion.cs     Caso de uso: PDF → test guardado
│   ├── IServicioParserExamen.cs    Contrato del parser geométrico
│   ├── ServicioAlmacenamiento.cs
│   ├── ServicioExtraccionPdf.cs
│   └── ServicioParserExamen.cs     ⭐ el parser geométrico
├── ViewModels/                 Presentación y estado (CommunityToolkit.Mvvm)
├── Views/                      XAML
│   ├── MainWindow.axaml        Shell: barra lateral + cabecera + contenido + modal
│   ├── HomeView.axaml          Inicio: métricas y carpetas
│   ├── FolderView.axaml        Carpetas: PDFs y acciones de práctica
│   └── QuizView.axaml          Modo examen
├── Assets/Estilos/
│   ├── Tema.axaml              Paleta (recursos)
│   └── Estilos.axaml           Estilos de controles
├── App.axaml                   Registro de recursos, estilos y ViewLocator
├── ViewLocator.cs              ViewModel → View por convención de nombre
├── ModoVerificacion.cs         Modos de consola (--verificar, --texto, --geometria, --analizar)
└── Program.cs                  Punto de entrada
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

## Notas técnicas (trampas encontradas)

- **`PdfPig`, no `UglyToad.PdfPig`.** El paquete de NuGet cambió de ID; el *namespace* sigue siendo
  `UglyToad.PdfPig`.
- **`Regex.Match()` nunca devuelve `null`.** Devuelve un `Match` con `Success == false`. Comprobar
  `match is not null` hace que **todo** case con el patrón. Hay que usar `match is { Success: true }`.
- **La Y de PDF crece hacia arriba.** El orden de lectura de una página es de Y **mayor** a menor, y el
  pie de página son las Y **pequeñas**.
- **`TextBox.Watermark` está obsoleto** en Avalonia 12: usa `PlaceholderText`.
- **Un `ResourceDictionary` no admite `<Style>`.** Los estilos van en un fichero con raíz `<Styles>` que se
  incluye con `StyleInclude`; los recursos (pinceles) van en otro con raíz `<ResourceDictionary>`.
- **`Application.Resources` necesita un `ResourceDictionary` intermedio** para poder usar
  `ResourceDictionary.MergedDictionaries`.
- **Windows Defender Application Control (WDAC)** de algunas empresas bloquea binarios gestionados sin
  firmar. Si el ejecutable no arranca, mira el log `Microsoft-Windows-CodeIntegrity/Operational`.

### Depuración

`RPASO_DEBUG_JSON=1` ya no aplica (no hay IA).

`RPASO_TEST_VISTA=carpeta|quiz|quiz-corregido` arranca directamente en una vista concreta (solo depuración).

---

## Límites conocidos

- **PDF escaneados**: sin capa de texto → 0 preguntas. Haría falta OCR.
- **PDF a dos columnas**: el parser asume una sola columna de texto. En un PDF a dos columnas las líneas
  se mezclarían.
- **Claves de respuesta**: si el PDF no incluye la clave, se usa la primera opción. Si el examen es del
  tipo *"señalale la respuesta incorrecta"*, la clave que trae el examen es la opción **incorrecta**, y así
  se guarda.
- **Exámenes sin numerar**: si no hay clave numerada, no se puede emparejar la clave con la pregunta.
