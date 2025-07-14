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

    public override Task OnTestClicked()
    {
        return CheckAtAddress(CurrentAddress);
    }

    public override async Task OnDiscoverClicked()
    {
        if (ModbusClient == null)
        {
            RaiseStatusChanged($"Connect to a serial port");
            return;
        }

        RaiseStatusChanged($"Searching for SPM1x...");
        await Task.Delay(1000);

        // we'll cheat and go direct
        for (byte address = 1; address < 255; address++)
        {
            try
            {
                var registers = await ModbusClient.ReadHoldingRegisters(address, 0, 8);
                if (registers.Length == 8)
                {
                    // this is SN/address
                    var sn = BitConverter.ToInt32(new byte[] { (byte)(registers[3] >> 8), (byte)(registers[3] & 0xFF), (byte)(registers[2] >> 8), (byte)(registers[2] & 0xFF) }, 0);
                    RaiseStatusChanged($"Device {registers[0]} found at address {registers[6]}");
                    return;
                }
            }
            catch (TimeoutException)
            {
                // no device at this address, keep looking
                RaiseStatusChanged($"Nothing at {address}");
            }
            catch (Exception ex)
            {
                RaiseStatusChanged(ex.Message);
            }
        }
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
