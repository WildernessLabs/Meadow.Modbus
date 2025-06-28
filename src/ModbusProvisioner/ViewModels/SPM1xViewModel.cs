using Meadow.Foundation.Sensors.Power;
using System;
using System.Threading.Tasks;

namespace ModbusProvisioner.ViewModels;

public class SPM1xViewModel : DeviceViewModel
{
    public const byte DiscoverAddress = 1;

    public override string DeviceName => "SPM1-X Power Meter";
    public override bool SupportsBaudRateChange => true;

    public SPM1xViewModel()
    {
    }

    private async Task CheckAtAddress(byte address)
    {
        if (address < 1)
        {
            RaiseStatusChanged($"Address must be 1-254");
            return;
        }

        if (ModbusClient == null)
        {
            RaiseStatusChanged($"Connect to a serial port");
            return;
        }

        try
        {
            RaiseStatusChanged($"Check address {address}...");
            var powerMeter = new Spm1x(ModbusClient, address);
            var baud = await powerMeter.GetBaudRate();
            CurrentAddress = powerMeter.ModbusAddress;
            RaiseStatusChanged($"Device found at address {powerMeter.ModbusAddress}");
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

    protected async override Task OnSetAddressClicked()
    {
        if (ModbusClient == null)
        {
            RaiseStatusChanged($"Connect to a serial port");
            return;
        }

        if (CurrentAddress < 1 || CurrentAddress > 254)
        {
            RaiseStatusChanged("Address must be 1-250");
            return;
        }

        if (NewAddress < 0 || NewAddress > 250)
        {
            RaiseStatusChanged("New address must be 1-250");
            return;
        }

        var powerMeter = new Spm1x(ModbusClient, CurrentAddress);

        int baud;
        try
        {
            baud = await powerMeter.GetBaudRate();
        }
        catch (TimeoutException)
        {
            RaiseStatusChanged($"No device at {CurrentAddress}");
            return;
        }

        if (NewAddress != 0 && powerMeter.ModbusAddress != NewAddress)
        {
            // this is going to time out waiting on a response
            try
            {
                RaiseStatusChanged("Setting address...");
                await powerMeter.SetModbusAddress(NewAddress);
            }
            catch (TimeoutException)
            {
                RaiseStatusChanged("Address changed.");
                CurrentAddress = NewAddress;
            }
            catch (Exception ex)
            {
                RaiseStatusChanged($"Error: {ex.Message}");
            }
        }

        if (NewBaudRate != null)
        {
            switch (NewBaudRate)
            {
                case 9600:
                case 19200:
                    try
                    {
                        // read the current rate to see if we can communicate with it
                        RaiseStatusChanged("Setting baud rate...");
                        await powerMeter.SetBaudRate(NewBaudRate.Value);
                    }
                    catch (TimeoutException)
                    {
                        RaiseStatusChanged("Rate changed. Close the serial port.");
                    }
                    catch (Exception ex)
                    {
                        RaiseStatusChanged($"Error: {ex.Message}");
                    }
                    break;
                default:
                    RaiseStatusChanged($"Devices supports only 9600, 19200");
                    break;
            }
        }
    }

}
