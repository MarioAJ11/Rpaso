using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Rpaso.ViewModels;

namespace Rpaso;

/// <summary>
/// Dado un ViewModel, devuelve la vista que le corresponde.
/// </summary>
/// <remarks>
/// Convención: <c>Rpaso.ViewModels.XxxViewModel</c> -&gt; <c>Rpaso.Views.XxxView</c>.
/// El nombre de la vista se obtiene sustituyendo "ViewModel" por "View", por eso las
/// clases de vista deben llamarse exactamente XxxView (y no XxxWindow).
/// </remarks>
public class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is null)
            return null;

        var tipo = Resolver(param.GetType());

        if (tipo is null)
            return new TextBlock { Text = $"Sin vista para {param.GetType().Name}" };

        return (Control)Activator.CreateInstance(tipo)!;
    }

    public bool Match(object? data)
    {
        if (data is not ViewModelBase)
            return false;

        // Solo hace match si la vista existe de verdad. Si no, la plantilla no secuestra
        // el elemento y Avalonia usa su representación por defecto.
        return Resolver(data.GetType()) is not null;
    }

    private static Type? Resolver(Type tipoViewModel)
    {
        var nombre = tipoViewModel.FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
        return Type.GetType(nombre);
    }
}
