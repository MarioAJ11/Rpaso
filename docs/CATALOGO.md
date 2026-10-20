# Catálogo de funciones

Referencia del alcance de la aplicación. La columna **Estado** refleja lo implementado hoy en la
versión nativa (Avalonia + C#), no la versión anterior de Electron.

Leyenda: ✅ implementado · ⚠️ parcial · ❌ pendiente

---

## 1. Lectura y parseo de exámenes

| Función | Estado | Notas |
| --- | --- | --- |
| Extraer texto y posiciones del PDF | ✅ | `PdfPig`: `Word.BoundingBox` da X, Y, base de línea y altura de letra |
| Reconocer PDFs escaneados con OCR | ✅ | OCR nativo de Windows (`Windows.Media.Ocr`), 0 MB extra. Solo Windows |
| Limpiar texto antes de analizarlo | ⚠️ | Se descartan márgenes (publicidad, avisos legales, marcas de agua). No se limpian encabezados repetidos entre páginas |
| Detectar preguntas y opciones | ✅ | Por etiqueta (`a)`, `b.`) **y** por sangría en X cuando no hay letra |
| Reconstruir texto partido | ✅ | Une continuación de enunciado y de opción por rango de X y salto vertical |
| Ordenar documentos a dos columnas | ❌ | No implementado. **Los PDFs propios revisados son de una columna** (verificado) |
| Encontrar respuestas correctas | ✅ | Clave numérica (`Respuestas: 1-b`), marcas del PDF (`Correcta:` / `Incorrecta:` / `Respuesta correcta` en el margen derecho) y, como último recurso, la opción en negrita |
| Conservar una alternativa de parseo | ❌ | No aplica: solo hay un parser |
| Informar de PDFs que no se pueden procesar | ✅ | El informe de importación dice el motivo: escaneo sin texto, archivo inválido, sin preguntas… |

### Detalle: cómo se detecta la respuesta correcta

1. Si el PDF trae una clave numerada (`Respuestas: 1-b, 2-c`), se usa esa.
2. Si no, se busca la marca explícita en el PDF (`Correcta:`, `Incorrecta:`, `Respuesta correcta`).
3. Si no hay ninguna, se busca la **única opción en negrita** de la pregunta.
4. Si no hay ninguna, se usa la primera opción.

> La negrita se detecta con `Letter.FontDetails.IsBold`, **no** con el nombre de la fuente:
> el prefijo de subconjunto (`DAAAAA+Times`) se asigna por orden de aparición y no indica nada.

### Detalle: OCR

Los PDFs escaneados se rasterizan con `Windows.Data.Pdf` y se leen con `Windows.Media.Ocr`.
Las palabras del OCR alimentan **el mismo parser geométrico** que los PDFs con capa de texto:
no hay un segundo camino de análisis. Es el motivo de que la app solo funcione en Windows
(TFM `net10.0-windows10.0.19041.0`).

---

## 2. Lógica de la aplicación

| Función | Estado | Notas |
| --- | --- | --- |
| Guardar datos localmente | ✅ | JSON en `%APPDATA%\Rpaso` (carpetas, tests, progreso) |
| Importar uno o varios PDF | ✅ | Selección múltiple y **informe de fallos** con el motivo concreto de cada uno |
| Asignar los exámenes a una carpeta | ✅ | |
| Actualizar un examen ya importado | ⚠️ | Se reemplaza por Id. No conserva los fallos previos de preguntas que coinciden |
| Crear, renombrar y eliminar carpetas | ✅ | Renombrar y eliminar con modal de confirmación; al eliminar, sus exámenes quedan "sin carpeta" (no se borra nada) |
| Filtrar por carpeta, todas o "sin carpeta" | ❌ | |
| Renombrar, revisar o eliminar exámenes | ✅ | Acciones en la tarjeta de cada examen |
| Seleccionar varios para combinarlos | ✅ | Casilla en cada examen + "Seleccionar todo / ninguno" |
| Editar preguntas | ✅ | Editor con enunciados, opciones, respuesta correcta, añadir/quitar y renumerado de letras |
| Practicar un examen individual | ✅ | |
| Practicar todos los de una carpeta | ✅ | |
| Practicar una selección combinada | ✅ | Mezcla las preguntas de varios exámenes en una sola práctica |
| Practicar el baúl de fallos de una carpeta | ✅ | |
| Práctica libre en exámenes sin soluciones | ❌ | Hoy se usa la primera opción como clave |
| Guardar y reanudar el progreso | ✅ | Se guarda el acierto/fallo, no la posición ni la selección actual |
| Baúl de fallos por carpeta | ✅ | Al acertar, la pregunta deja de figurar como fallo |
| Limpiar preguntas con Grok | ❌ | **Descartada por decisión**: la app se mantiene sin IA y sin claves de API |

---

## 3. Interfaz y presentación visual

| Función | Estado | Notas |
| --- | --- | --- |
| Inicio tipo panel | ✅ | Contadores de exámenes, preguntas y fallos + tarjetas de carpetas |
| Biblioteca visual | ✅ | Tarjetas con nº de preguntas, fecha, carpeta y acciones |
| Navegación por vistas | ✅ | Inicio / carpeta / práctica / editor, con migas y resaltado del menú |
| Editor de preguntas | ✅ | Vista propia con cabecera y acciones |
| Modo práctica | ✅ | Una pregunta por pantalla, opciones, progreso y feedback |
| Diálogos y avisos | ✅ | Modal genérico reutilizado para crear carpeta, renombrar y eliminar (el botón de eliminar se pinta en rojo) |
| Sistema visual consistente | ✅ | Barra lateral, tarjetas, colores por carpeta, estados de acierto/error |

---

## Pendiente prioritario (por valor)

### 1. Filtrar por "sin carpeta" y agrupar por materia

Hoy los exámenes sin carpeta se quedan en la carpeta vacía: se ven al abrir la carpeta
correspondiente, pero no hay una vista propia que los agrupe. Es el siguiente hueco de la
biblioteca, junto con la exportación.

### 2. Exportar / compartir un examen

No hay forma de sacar un test del programa (ni PDF, ni JSON, ni texto plano). Para repasar
fuera del ordenador sigue haciendo falta el PDF original.

### 3. Práctica libre en exámenes sin soluciones

Cuando el PDF no trae claves, hoy se usa la primera opción como clave. Falta un modo en el que
el usuario responda sin nota y revise al final.

---

## Nota sobre la arquitectura

La versión anterior era Electron (PDF.js + Tesseract.js). Esta versión es nativa (Avalonia + C#)
y mantiene el mismo modelo mental:

| Electron | Avalonia / C# |
| --- | --- |
| PDF.js → `getTextContent()` con transforms | `PdfPig` → `Word.BoundingBox` |
| OCR con Tesseract.js | `Windows.Media.Ocr` (nativo de Windows) |
| Componentes React | `UserControl` en XAML + `ViewLocator` |
| `useState` | `[ObservableProperty]` (CommunityToolkit.Mvvm) |
| `localStorage` | JSON en `%APPDATA%` |
