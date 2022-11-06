using Meadow;
using Meadow.Devices;
using Meadow.Hardware;
using Meadow.Modbus;
using System;
using System.Threading.Tasks;

namespace F7v2RtuSample
{
    public class MeadowApp : App<F7FeatherV2>
    {
        private ModbusRtuClient _client;

        public override Task Initialize()
        {
            Resolver.Log.Info("Initialize hardware...");

            var port = Device.CreateSerialPort(Device.SerialPortNames.Com4, 19200, 8, Meadow.Hardware.Parity.None, Meadow.Hardware.StopBits.One);
            port.WriteTimeout = port.ReadTimeout = TimeSpan.FromSeconds(5);

            var projLab = new ProjectLab();
            IDigitalOutputPort serialEnable;

            if (projLab.IsV1Hardware())
            {
                Resolver.Log.Info("ProjectLab v1 detected");
                serialEnable = Device.CreateDigitalOutputPort(Device.Pins.D09, false); // early ProjLab and Hack board
            }
            else
            {
                Resolver.Log.Info("ProjectLab v2 detected");
                serialEnable = projLab.Mcp_2.CreateDigitalOutputPort(projLab.Mcp_2.Pins.GP0, false);
            }

            Resolver.Log.Info("Creating the Modbus RTU Enable port");
            _client = new ModbusRtuClient(port, serialEnable);

            return Task.CompletedTask;
        }

        public override Task Run()
        {
            _client.Connect();
            return Task.Run(() => DoReading(_client));
        }

        private async Task DoReading(IModbusBusClient client)
        {
            try
            {
                // read current set point
                byte address = 201;
                ushort setPointRegister = 345; // occupied setpoint, in tenths of a degree
                ushort tempRegister = 121; // current temp, in tenths of a degree

                var read = false;
                ushort[] registers = null;

                do
                {
                    Console.WriteLine($"Reading thermostat holding registers...");

                    try
                    {
                        registers = await client.ReadHoldingRegisters(address, setPointRegister, 1);
                        Console.WriteLine($"Current set point: {registers[0] / 10f}");
                        read = true;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Reading failed: {ex.Message}");
                        await Task.Delay(1000);
                    }
                } while (!read);

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
