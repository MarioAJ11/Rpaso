using System.Text;
using Rpaso.Models;

namespace Rpaso.Services;

/// <summary>
/// Representación en texto plano de un test: cada pregunta con sus opciones juntas y la
/// correcta marcada con <c>[x]</c>. Sirve para revisar de un vistazo lo que ha entendido
/// el parser antes de ponerse a practicar.
/// </summary>
public static class TextoPlano
{
    public static string Generar(Test test)
    {
        var texto = new StringBuilder();

        for (var i = 0; i < test.Preguntas.Count; i++)
        {
            var pregunta = test.Preguntas[i];

            texto.AppendLine($"{i + 1}. {pregunta.Enunciado}");

            foreach (var opcion in pregunta.Opciones)
            {
                var marca = string.Equals(opcion.Id, pregunta.ClaveRespuesta, StringComparison.OrdinalIgnoreCase)
                    ? "[x]"
                    : "[ ]";

                texto.AppendLine($"   {marca} {opcion.Id}) {opcion.Texto}");
            }

            texto.AppendLine();
        }

        return texto.ToString().TrimEnd() + Environment.NewLine;
    }
}
