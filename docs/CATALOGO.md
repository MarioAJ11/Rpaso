# Catálogo de funciones

Referencia del alcance de la aplicación. La columna **Estado** refleja lo implementado hoy en la
versión nativa (Avalonia + C#), no la versión anterior de Electron.

Leyenda: ✅ implementado · 🟡 parcial · ⬜ pendiente

---

## 1. Lectura y parseo de exámenes

| Función | Estado | Notas |
| --- | --- | --- |
| Extraer texto y posiciones del PDF | ✅ | `PdfPig`: `Word.BoundingBox` da X, Y, base de línea y altura de letra |
| Reconocer PDFs escaneados con OCR | ⬜ | Requiere motor de OCR nativo (ver "Pendiente prioritario") |
| Limpiar texto antes de analizarlo | 🟡 | Se descartan márgenes (publicidad, avisos legales, marcas de agua). No se limpian encabezados repetidos entre páginas |
| Detectar preguntas y opciones | ✅ | Por etiqueta (`a)`, `b.`) **y** por sangría en X cuando no hay letra |
| Reconstruir texto partido | ✅ | Une continuación de enunciado y de opción por rango de X y salto vertical |
| Ordenar documentos a dos columnas | ⬜ | No implementado. **Los PDFs propios revisados son de una columna** (verificado) |
| Encontrar respuestas correctas | ✅ | Clave numérica (`Respuestas: 1-b`), marcas del PDF (`Correcta:` / `Incorrecta:` / `Respuesta correcta` en el margen derecho) y, como último recurso, la opción en negrita |
| Conservar una alternativa de parseo | ⬜ | No aplica: solo hay un parser |
| Informar de PDFs que no se pueden procesar | ✅ | El informe de importación dice el motivo: escaneo sin texto, archivo inválido, sin preguntas… |

### Detalle: cómo se detecta la respuesta correcta

1. Si el PDF trae una clave numerada (`Respuestas: 1-b, 2-c`), se usa esa.
2. Si no, se busca la **única opción en negrita** de la pregunta. En los tests de wuolah la
   correcta va en negrita.
3. Si no hay ninguna, se usa la primera opción.

> La negrita se detecta con `Letter.FontDetails.IsBold`, **no** con el nombre de la fuente:
> el prefijo de subconjunto (`DAAAAA+Times`) se asigna por orden de aparición y no indica nada.

---

## 2. Lógica de la aplicación

| Función | Estado | Notas |
| --- | --- | --- |
| Guardar datos localmente | ✅ | JSON en `%APPDATA%\Rpaso` (carpetas, tests, progreso) |
| Importar uno o varios PDF | ✅ | Selección múltiple y **informe de fallos** con el motivo concreto de cada uno |
| Asignar los exámenes a una carpeta | ✅ | |
| Actualizar un examen ya importado | 🟡 | Se reemplaza por Id. No conserva los fallos previos de preguntas que coinciden |
| Crear, renombrar y eliminar carpetas | 🟡 | Crear ✅ · renombrar ⬜ · eliminar ⬜ |
| Filtrar por carpeta, todas o "sin carpeta" | ⬜ | |
| Renombrar, revisar o eliminar exámenes | ⬜ | Solo se pueden practicar |
| Seleccionar varios para combinarlos | ⬜ | |
| Editar preguntas | ⬜ | |
| Practicar un examen individual | ✅ | |
| Practicar todos los de una carpeta | ✅ | |
| Practicar una selección combinada | ⬜ | |
| Practicar el baúl de fallos de una carpeta | ✅ | |
| Práctica libre en exámenes sin soluciones | ⬜ | Hoy se usa la primera opción como clave |
| Guardar y reanudar el progreso | ⬜ | Se guarda el acierto/fallo, no la posición ni la selección actual |
| Baúl de fallos por carpeta | ✅ | Al acertar, la pregunta deja de figurar como fallo |
| Limpiar preguntas con Grok | ❌ | **Descartada por decisión**: la app se mantiene sin IA y sin claves de API |

---

## 3. Interfaz y presentación visual

| Función | Estado | Notas |
| --- | --- | --- |
| Inicio tipo panel | ✅ | Contadores de exámenes, preguntas y fallos + tarjetas de carpetas |
| Biblioteca visual | ✅ | Tarjetas con nº de preguntas, fecha, carpeta y acciones |
| Navegación por vistas | ✅ | Inicio / carpeta / práctica, con migas y resaltado del menú |
| Editor de preguntas | ⬜ | |
| Modo práctica | ✅ | Una pregunta por pantalla, opciones, progreso y feedback |
| Diálogos y avisos | ✅ | Modal de carpeta. Falta el de renombrar examen y el de clave API |
| Sistema visual consistente | ✅ | Barra lateral, tarjetas, colores por carpeta, estados de acierto/error |

---

## Pendiente prioritario (por valor)

### 1. OCR para PDFs escaneados

Hoy 2 de tus PDFs dan **0 preguntas** porque son escaneos sin capa de texto
(`TEST DE LA PLATAFORMA.pdf`, `TEST PLATAFORMA 2.pdf`).

Opciones en .NET, de menos a más peso para el ejecutable:

| Opción | Peso | Notas |
| --- | --- | --- |
| `Windows.Media.Ocr` (WinRT) | 0 MB | Ya está en Windows 10/11. **Obliga a cambiar el TFM a `net10.0-windows10.0.x`**: la app pasa a ser solo Windows |
| `Tesseract` (tesseract.net) | ~50 MB | Multiplataforma. Hay que empaquetar los binarios nativos y `spa.traineddata` |
| PaddleOCR / ONNX | ~100 MB+ | Mejor precisión en documentos, mucho más complejo |

### 2. Editor de preguntas

Es lo que convierte la app en algo usable de verdad: sin editor, una pregunta mal parseada no se
puede arreglar, y sin clave las claves incorrectas no se pueden corregir.

### 3. Importación múltiple con informe de fallos

~~Pendiente~~ ✅ **Hecho**: se selectionan varios PDFs de golpe y al final aparece un informe
con los importados (y cuántas preguntas) y los fallidos (con el motivo exacto). Un archivo
que falla no detiene el resto.

### 4. Renombrar / eliminar carpetas y exámenes

El mínimo para gestionar la biblioteca.

---

## Nota sobre la arquitectura

La versión anterior era Electron (PDF.js + Tesseract.js). Esta versión es nativa (Avalonia + C#)
y mantiene el mismo modelo mental:

| Electron | Avalonia / C# |
| --- | --- |
| PDF.js → `getTextContent()` conTransforms | `PdfPig` → `Word.BoundingBox` |
| OCR con Tesseract.js | Pendiente de decidir motor |
| Componentes React | `UserControl` en XAML + `ViewLocator` |
| `useState` | `[ObservableProperty]` (CommunityToolkit.Mvvm) |
| `localStorage` | JSON en `%APPDATA%` |
