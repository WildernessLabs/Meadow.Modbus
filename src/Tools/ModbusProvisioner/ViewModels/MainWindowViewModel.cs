using Meadow.Modbus;
using ReactiveUI;
using System;
using System.Linq;
using System.Windows.Input;

namespace ModbusProvisioner.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly SerialPortService _portService;

    private string _typeLabel = "K114 Transducer";
    private string _portActionText = "Open";
    private string[] _ports;
    private string? _selectedPort;
    private ModbusRtuClient? _modbusClient;

    private K114ViewModel? _kellerViewModel;
    private Y4000ViewModel? _y4000ViewModel;
    private V10xViewModel? _v10xViewModel;
    private SPM1xViewModel? _sPM1ViewModel;
    private T322ViewModel? _t322ViewModel;
    private DeviceViewModel? _activeDeviceViewModel;
    private string? _status;
    private int _selectedRate = 9600;

    public ICommand K114ClickCommand { get; }
    public ICommand Y4000ClickCommand { get; }
    public ICommand PortActionCommand { get; }
    public ICommand RefreshPortsCommand { get; }
    public ICommand V10xClickCommand { get; }
    public ICommand SPM1xClickCommand { get; }
    public ICommand T3ClickCommand { get; }

    public DeviceViewModel DeviceViewModel
    {
        get => _activeDeviceViewModel;
        private set
        {
            if (_activeDeviceViewModel != null)
            {
                _activeDeviceViewModel.StatusChanged -= OnDeviceViewModelStatusChanged;
            }
            _activeDeviceViewModel = value;
            _activeDeviceViewModel.StatusChanged += OnDeviceViewModelStatusChanged;
            this.RaisePropertyChanged();
        }
    }

    private void OnDeviceViewModelStatusChanged(object? sender, string e)
    {
        Status = e;
    }

    public MainWindowViewModel() // required for designer
    {
        _kellerViewModel = new K114ViewModel();
        DeviceViewModel = _kellerViewModel;
    }

    public MainWindowViewModel(SerialPortService sps)
        : this()
    {
        _portService = sps;

        K114ClickCommand = ReactiveCommand.Create(OnK114Click);
        Y4000ClickCommand = ReactiveCommand.Create(OnY4000Click);
        V10xClickCommand = ReactiveCommand.Create(OnV10xClick);
        PortActionCommand = ReactiveCommand.Create(OnPortActionClick);
        RefreshPortsCommand = ReactiveCommand.Create(OnRefreshPortsClick);
        SPM1xClickCommand = ReactiveCommand.Create(OnSPM1xClick);
        T3ClickCommand = ReactiveCommand.Create(OnT3Click);

        RefreshPorts();
    }

    public string[] Ports
    {
        get => _ports;
        private set => this.RaiseAndSetIfChanged(ref _ports, value);
    }

    public int[] BaudRates => [9600, 19200];

    public int SelectedBaudRate
    {
        get => _selectedRate;
        private set => this.RaiseAndSetIfChanged(ref _selectedRate, value);
    }

    public string? SelectedPort
    {
        get => _selectedPort;
        private set => this.RaiseAndSetIfChanged(ref _selectedPort, value);
    }

    public string DeviceTypeLabel
    {
        get => _typeLabel;
        private set => this.RaiseAndSetIfChanged(ref _typeLabel, value);
    }

    public string PortActionText
    {
        get => _portActionText;
        private set => this.RaiseAndSetIfChanged(ref _portActionText, value);
    }

    public string? Status
    {
        get => _status;
        private set => this.RaiseAndSetIfChanged(ref _status, value);
    }

    private void RefreshPorts()
    {
        Status = "Refreshing serial ports...";
        Ports = _portService.GetPortNames().ToArray();
        Status = $"{Ports.Length} ports found";
    }

    private void OnRefreshPortsClick()
    {
        RefreshPorts();
    }

    private void OnPortActionClick()
    {
        if (_modbusClient == null)
        {
            if (_selectedPort == null) return;

            try
            {
                var p = new SerialPortShim(_selectedPort, SelectedBaudRate, Meadow.Hardware.Parity.None, 8, Meadow.Hardware.StopBits.One);
                _modbusClient = new ModbusRtuClient(p, TimeSpan.FromMilliseconds(500));
                _modbusClient.Connect();

                DeviceViewModel.ModbusClient = _modbusClient;

                PortActionText = "Close";
                Status = "Serial port opened";
            }
            catch (Exception ex)
            {
                Status = ex.Message;
            }
        }
        else
        {
            _modbusClient.Disconnect();
            _modbusClient.Dispose();
            _modbusClient = null;
            PortActionText = "Open";
            Status = "Serial port closed";
        }
    }

    private void OnT3Click()
    {
        if (_t322ViewModel == null)
        {
            _t322ViewModel = new T322ViewModel();
        }

        DeviceViewModel = _t322ViewModel;
        DeviceViewModel.ModbusClient = this._modbusClient;
    }

    private void OnSPM1xClick()
    {
        if (_sPM1ViewModel == null)
        {
            _sPM1ViewModel = new SPM1xViewModel();
        }

        DeviceViewModel = _sPM1ViewModel;
        DeviceViewModel.ModbusClient = this._modbusClient;
    }

    private void OnY4000Click()
    {
        if (_y4000ViewModel == null)
        {
            _y4000ViewModel = new Y4000ViewModel();
        }

        DeviceViewModel = _y4000ViewModel;
        DeviceViewModel.ModbusClient = this._modbusClient;
    }

    private void OnK114Click()
    {
        if (_kellerViewModel == null)
        {
            _kellerViewModel = new K114ViewModel();
        }

        DeviceViewModel = _kellerViewModel;
        DeviceViewModel.ModbusClient = this._modbusClient;
    }

    private void OnV10xClick()
    {
        if (_v10xViewModel == null)
        {
            _v10xViewModel = new V10xViewModel();
        }

        DeviceViewModel = _v10xViewModel;
        DeviceViewModel.ModbusClient = this._modbusClient;
    }
}
