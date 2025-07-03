using Avalonia.Controls;
using ModbusProvisioner.ViewModels;

namespace ModbusProvisioner.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow() // for the designer
        {
            InitializeComponent();
        }

        public MainWindow(MainWindowViewModel vm)
            : this()
        {
            this.DataContext = vm;
        }
    }
}