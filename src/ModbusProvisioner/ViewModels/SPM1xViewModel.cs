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

    public override async Task OnDiscoverClicked()
    {
        if (ModbusClient == null)
        {
            RaiseStatusChanged($"Connect to a serial port");
            return;
        }

        try
        {
            RaiseStatusChanged($"Searching...");
            var powerMeter = new Spm1x(ModbusClient, DiscoverAddress);
            var baud = await powerMeter.GetBaudRate();
            CurrentAddress = powerMeter.ModbusAddress;
            RaiseStatusChanged($"Device found at address {CurrentAddress}");
        }
        catch (Exception ex)
        {
            RaiseStatusChanged(ex.Message);
        }
    }

    protected async override Task OnSetAddressClicked()
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

        var powerMeter = new Spm1x(ModbusClient, DiscoverAddress);

        var baud = await powerMeter.GetBaudRate();

        if (powerMeter.ModbusAddress != NewAddress)
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
                        RaiseStatusChanged("Setting baud rate...");
                        await powerMeter.SetModbusAddress(NewAddress);
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
