using Meadow.Modbus;
using ReactiveUI;
using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ModbusProvisioner.ViewModels;

public abstract class DeviceViewModel : ViewModelBase
{
    private byte _currentAddress;
    private byte _newAddress;
    private int? _newbaudRate;

    public event EventHandler<byte>? DeviceDiscovered;
    public event EventHandler<string>? StatusChanged;

    public ICommand SetAddressCommand { get; }
    public ICommand DiscoverCommand { get; }

    public DeviceViewModel()
    {
        SetAddressCommand = ReactiveCommand.CreateFromTask(OnSetAddressClicked);
        DiscoverCommand = ReactiveCommand.CreateFromTask(OnDiscoverClicked);
    }

    protected abstract Task OnSetAddressClicked();
    public abstract Task OnDiscoverClicked();

    public abstract string DeviceName { get; }
    public virtual bool SupportsBaudRateChange => false;

    public ModbusRtuClient? ModbusClient { get; set; }

    public byte CurrentAddress
    {
        get => _currentAddress;
        set => this.RaiseAndSetIfChanged(ref _currentAddress, value);
    }

    public int? NewBaudRate
    {
        get => _newbaudRate;
        set => this.RaiseAndSetIfChanged(ref _newbaudRate, value);
    }

    public byte NewAddress
    {
        get => _newAddress;
        set => this.RaiseAndSetIfChanged(ref _newAddress, value);
    }

    protected void RaiseStatusChanged(string status)
    {
        StatusChanged?.Invoke(this, status);
    }
}
