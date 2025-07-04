using Meadow.Foundation.IOExpanders;
using System;
using System.Threading.Tasks;

namespace ModbusProvisioner.ViewModels;

public class T322ViewModel : DeviceViewModel
{
    public const byte DiscoverAddress = 1;

    public override string DeviceName => "T3-22ai I/O Module";
    public override bool SupportsBaudRateChange => true;

    public T322ViewModel()
    {
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
            RaiseStatusChanged($"Checking address {address}...");
            var module = new T322ai(ModbusClient, address);
            var sn = await module.ReadSerialNumber();
            CurrentAddress = module.ModbusAddress;
            RaiseStatusChanged($"Device found at address {module.ModbusAddress}");
        }
        catch (Exception ex)
        {
            RaiseStatusChanged(ex.Message);
        }
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

        var module = new T322ai(ModbusClient, CurrentAddress);

        int sn;
        try
        {
            sn = await module.ReadSerialNumber();
        }
        catch (TimeoutException)
        {
            RaiseStatusChanged($"No device at {CurrentAddress}");
            return;
        }

        if (NewAddress != 0 && module.ModbusAddress != NewAddress)
        {
            // this is going to time out waiting on a response
            try
            {
                RaiseStatusChanged("Setting address...");
                await module.WriteModbusAddress(NewAddress);
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
                case 38400:
                case 57600:
                case 115200:
                    try
                    {
                        // read the current rate to see if we can communicate with it
                        RaiseStatusChanged("Setting baud rate...");
                        await module.WriteBaudRate(NewBaudRate.Value);
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
                    RaiseStatusChanged($"Devices supports only 9600, 19200, 38400, 57600, 19200");
                    break;
            }
        }
    }
}