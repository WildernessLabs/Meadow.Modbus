using Avalonia.Controls;
using Avalonia.Controls.Templates;
using ModbusProvisioner.ViewModels;
using System;

namespace ModbusProvisioner
{
    public class ViewLocator : IDataTemplate
    {
        public bool Match(object data)
        {
            return data is ViewModelBase;
        }

        Control? ITemplate<object?, Control?>.Build(object? param)
        {
            if (param == null) return null;

            var type = param.GetType();
            var name = type.FullName!.Replace("ViewModel", "View");

            if (type != null)
            {
                return (Control)Activator.CreateInstance(type)!;
            }

            return new TextBlock { Text = "Not Found: " + name };
        }
    }
}