using Meadow;
using Meadow.Devices;
using Meadow.Foundation;
using Meadow.Foundation.Leds;
using Meadow.Modbus;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace F7v2RtuSample
{
    public class MeadowApp : App<F7MicroV2, MeadowApp>
    {
        public MeadowApp()
        {
            var client = Initialize();
            client.Connect();
            Task.Run(() => DoReading(client));
        }

        IModbusBusClient Initialize()
        {
            Console.WriteLine("Initialize hardware...");

            var port = Device.CreateSerialPort(Device.SerialPortNames.Com4, 19200, 8, Meadow.Hardware.Parity.None, Meadow.Hardware.StopBits.One);
            port.WriteTimeout = port.ReadTimeout = 5000; // TimeSpan.FromSeconds(5);
            var enable = Device.CreateDigitalOutputPort(Device.Pins.D02, false);
            return new ModbusRtuClient(port, enable);
        }

        private async Task DoReading(IModbusBusClient client)
        {
            try
            {
                // read current set point
                byte address = 201;
                ushort setPointRegister = 345; // occupied setpoint, in tenths of a degree
                ushort tempRegister = 121; // current temp, in tenths of a degree

                Console.WriteLine($"Reading thermostat holding registers...");

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

                while (true)
                {
                    registers = await client.ReadHoldingRegisters(address, tempRegister, 1);

                    Console.WriteLine($"Current temp: {registers[0] / 10f}");

                    await Task.Delay(TimeSpan.FromSeconds(5));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
    }
}
