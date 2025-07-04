using Meadow.Foundation.Sensors.Environmental;
using System;
using System.Threading.Tasks;

namespace ModbusProvisioner.ViewModels;

public class K114ViewModel : DeviceViewModel
{
    public const byte DiscoverAddress = 250;

    public override string DeviceName => "K114 Transducer";

    public K114ViewModel()
    {
    }

    protected override async Task OnSetAddressClicked()
    {
        if (ModbusClient == null) { return; }

        if (CurrentAddress < 1 || CurrentAddress > 250)
        {
            RaiseStatusChanged("Current address must be 1-250");
            return;
        }

        if (NewAddress < 1 || NewAddress > 250)
        {
            RaiseStatusChanged("New address must be 1-250");
            return;
        }

        if (CurrentAddress == NewAddress)
        {
            RaiseStatusChanged("Addresses must be different");
            return;
        }

        try
        {
            var t = new KellerTransducer(ModbusClient, CurrentAddress);
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

    public override Task OnTestClicked()
    {
        return CheckAtAddress(CurrentAddress);
    }

    public override Task OnDiscoverClicked()
    {
        return CheckAtAddress(DiscoverAddress);
    }

    private async Task CheckAtAddress(byte address)
    {
        if (ModbusClient == null)
        {
            RaiseStatusChanged($"Connect to a serial port");
            return;
        }

        // query the modbus address
        try
        {
            var t = new KellerTransducer(ModbusClient, address);
            RaiseStatusChanged($"Searching...");
            CurrentAddress = await t.ReadModbusAddress();
            RaiseStatusChanged($"Device found at address {address}");


            // TODO: query the serial number?
            //var sn = await t.ReadSerialNumber();
            //ModbusSerialNumber = sn.ToString();
        }
        catch (Exception ex)
        {
            RaiseStatusChanged(ex.Message);
        }
    }
}
