<img src="design/banner.jpg" style="margin-bottom:10px" />

## Repo Status

[![Build Modbus](https://github.com/WildernessLabs/Meadow.Modbus/actions/workflows/build.yml/badge.svg)](https://github.com/WildernessLabs/Meadow.Modbus/actions/workflows/build.yml)    
[![Modbus Unit Tests](https://github.com/WildernessLabs/Meadow.Modbus/actions/workflows/tests.yml/badge.svg)](https://github.com/WildernessLabs/Meadow.Modbus/actions/workflows/tests.yml)  
[![NuGet Package Creation](https://github.com/WildernessLabs/Meadow.Modbus/actions/workflows/package.yml/badge.svg)](https://github.com/WildernessLabs/Meadow.Modbus/actions/workflows/package.yml)

## Supported Platforms and Distributions

`Meadow.Modbus` is designed to be cross-platform, and does not require the use of `Meadow.Core`, meaning that it can be used in pretty much any modern .NET application.

It has been verified on hardware on the following platforms:

| Feature | Platform | Notes |
| :---: | :---:| :---: |
| Modbus RTU Client | Meadow Feather F7 | b6.2 or later |
| Modbus RTU Client | Windows 10 | .NET 6 |
| Modbus RTU Client | Raspberry Pi, Raspbian Buster | .NET 5, .NET 6 and Mono |
| Modbus TCP Client | Windows 10 | .NET 6 |
| Modbus TCP Client | Raspberry Pi, Raspbian Buster | .NET 5, .NET 6 and Mono |
| Modbus TCP Server | Windows 10 | .NET 6 |

## License

Apache 2.0

See [LICENSE File](/LICENSE)

## Examples

All samples use the `Meadow.Modbus` library, available in NuGet:

```
PM> Install-Package Meadow.Modbus
```

### Modbus RTU Client

> This sample assumes you have a Thermostat connected over RS485 and configured to be at Modbus address 201.

Create a ModbusRtuClient instance, passing in the SerialPort for COM4 and DigitalOutputPort used for the enable.

```
var port = Device.CreateSerialPort(Device.SerialPortNames.Com4, 19200, 8, Meadow.Hardware.Parity.None, Meadow.Hardware.StopBits.One);
port.WriteTimeout = port.ReadTimeout = TimeSpan.FromSeconds(5);
var enable = Device.CreateDigitalOutputPort(Device.Pins.D02, false);
var client = new ModbusRtuClient(port, enable);
The Temco TSTAT8 uses holding registers for all of its interfacing. It has a lot of registers for reading or controlling just about every aspect of its operation. For our purposes we’ll only look at two of them: current temperature and current occupied setpoint. Since Modbus holding registers are ushort values, but the actual temperature and setpoint are in tenths of a degree, we have to do scaling in our application. For example a current temperature register reading of 721 equates to a temeprature of 72.1 degrees. Similarly if we want to set a setpoint of 69.5 degrees, we write 695.
```

To read the current temperature and output it to the console every 5 seconds, we can use a loop like this:

```
byte address = 201;

ushort tempRegister = 121; // current temp, in tenths of a degree

while (true)
{
    registers = await client.ReadHoldingRegisters(address, tempRegister, 1);

    Console.WriteLine($"Current temp: {registers[0] / 10f}");

    await Task.Delay(TimeSpan.FromSeconds(5));
}
```

Writing to a holding register is similar to the read shown above. Below is code that reads the setpoint, changes it with a write, then re-reads to verify the change:

```
byte address = 201;
ushort setPointRegister = 345; // occupied setpoint, in tenths of a degree


Console.WriteLine($"Reading setpoint holding register...");

var registers = await client.ReadHoldingRegisters(address, setPointRegister, 1);

Console.WriteLine($"Current set point: {registers[0] / 10f}");

var r = new Random();
var delta = r.Next(-20, 20);
var newSetpoint = (ushort)(registers[0] + delta);

Console.WriteLine($"Changing set point to: {newSetpoint / 10f}...");

await client.WriteHoldingRegister(address, setPointRegister, newSetpoint);

Console.WriteLine($"Re-reading thermostat holding registers...");

registers = await client.ReadHoldingRegisters(address, setPointRegister, 1);

Console.WriteLine($"Current set point: {registers[0] / 10f}");
```

### Modbus TCP Client

> Coming Soon!

### Modbus TCP Server

> Coming Soon!
