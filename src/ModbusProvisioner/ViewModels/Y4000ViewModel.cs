using Meadow.Foundation.Sensors.Environmental;
using System;
using System.Threading.Tasks;

namespace ModbusProvisioner.ViewModels;

public class Y4000ViewModel : DeviceViewModel
{
    public override string DeviceName => "Y4000 Sonde";

    public Y4000ViewModel()
    {
    }

    public override async Task OnDiscoverClicked()
    {
        if (ModbusClient == null) { return; }

        // query the modbus address
        try
        {
            var t = new Y4000(ModbusClient, CurrentAddress);
            RaiseStatusChanged($"Checking {CurrentAddress}...");
            var sn = await t.GetSerialNumber();
            // TODO: verify the SN is a Y4000?
            RaiseStatusChanged($"Device {sn} found at address {CurrentAddress}");
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
            var t = new Y4000(ModbusClient, CurrentAddress);
            RaiseStatusChanged($"Setting address to {NewAddress}...");
            await t.SetISDN(NewAddress);
            RaiseStatusChanged($"Address set to {NewAddress}");
            CurrentAddress = NewAddress;
        }
        catch (Exception ex)
        {
            RaiseStatusChanged(ex.Message);
        }
    }
}
