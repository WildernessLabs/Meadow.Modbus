using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using ModbusProvisioner.ViewModels;
using ModbusProvisioner.Views;
using System;

namespace ModbusProvisioner
{
    public partial class App : Application
    {
        private IServiceProvider _serviceProvider;

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);

            _serviceProvider = ConfigureServices();
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            }

            base.OnFrameworkInitializationCompleted();
        }

        public IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            services.AddTransient<MainWindow>();
            services.AddTransient<MainWindowViewModel>();

            services.AddSingleton<DeviceService>();
            services.AddSingleton<SerialPortService>();

            return services.BuildServiceProvider();
        }
    }
}