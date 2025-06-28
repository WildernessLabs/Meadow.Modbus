using Meadow.Foundation.Batteries.Voltaic;
using Meadow.Modbus;
using System;
using System.Threading.Tasks;

namespace ModbusProvisioner.ViewModels;

public class V10xViewModel : DeviceViewModel
{
    public const byte DiscoverAddress = 254;

    public override string DeviceName => "V10x Battery Controller";

    public V10xViewModel()
    {
    }

    public override async Task OnDiscoverClicked()
    {
        if (ModbusClient == null) { return; }

        // query the modbus address
        try
        {
            var t = new V10x(ModbusClient, DiscoverAddress);
            RaiseStatusChanged($"Searching...");
            CurrentAddress = await t.ReadModbusAddress();
            RaiseStatusChanged($"Device found at address {CurrentAddress}");
        }
        catch (ModbusException mex)
        {
            RaiseStatusChanged($"Modbus Exception: {mex.ErrorCode}");
        }
        catch (Exception ex)
        {
            RaiseStatusChanged(ex.Message);
        }
    }

    protected override async Task OnSetAddressClicked()
    {
        if (ModbusClient == null) { return; }

        if (CurrentAddress < 1 || CurrentAddress > 247)
        {
            RaiseStatusChanged("Current address must be 1-247");
            return;
        }

        if (NewAddress < 1 || NewAddress > 247)
        {
            RaiseStatusChanged("New address must be 1-247");
            return;
        }

        if (CurrentAddress == NewAddress)
        {
            RaiseStatusChanged("Addresses must be different");
            return;
        }

        try
        {
            var t = new V10x(ModbusClient, CurrentAddress);
            RaiseStatusChanged($"Setting address to {NewAddress}...");
            await t.WriteModbusAddress(NewAddress);
            RaiseStatusChanged($"Address set to {NewAddress}");
            CurrentAddress = NewAddress;
        }
        catch (Exception ex)
        {
            RaiseStatusChanged(ex.Message);
        }
    }
}
