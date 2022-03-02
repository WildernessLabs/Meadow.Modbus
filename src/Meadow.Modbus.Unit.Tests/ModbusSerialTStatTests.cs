using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Xunit;

namespace Meadow.Modbus.Unit.Tests
{
    public class ModbusSerialTStatTests
    {
        // this class assumes a connected serial Temco Controls TSTAT7 or TSTAT8
        /*
        [Fact]
        public async void ReadHoldingRegisterTest()
        {
            using (var port = new SerialPortShim("COM4", 19200, Hardware.Parity.None, 8, Hardware.StopBits.One))
            {
                port.ReadTimeout = TimeSpan.FromSeconds(15);
                port.Open();

                byte address = 201;
                ushort setpointRegister = 40346;
                ushort tempRegister = 40122;
                var readCount = 10;

                var client = new ModbusRtuClient(port);

                for (ushort i = 0; i < readCount; i++)
                {
                    Debug.WriteLine("<--");
                    var setpoint = await client.ReadHoldingRegisters(address, setpointRegister, 1);
                    var temp = await client.ReadHoldingRegisters(address, tempRegister, 1);
                    await Task.Delay(1000);
                }
            }
        }

        [Fact]
        public async void WriteHoldingRegisterTest()
        {
            using (var port = new SerialPortShim("COM4", 19200, Hardware.Parity.None, 8, Hardware.StopBits.One))
            {
                port.ReadTimeout = TimeSpan.FromSeconds(15);
                port.Open();

                byte address = 201;
                ushort startRegister = 345; // occupied setpoint, in tenths of a degree
                var writeCount = 10;
                ushort startValue = 610;

                var client = new ModbusRtuClient(port);

                for (ushort i = 0; i < writeCount; i++)
                {
                    Debug.WriteLine("-->");
                    await client.WriteHoldingRegister(address, startRegister, startValue);
                    startValue += 10;
                    await Task.Delay(1000);
                }
            }
        }

        [Fact]
        public async void OverlappingAccessTest()
        {
            using (var port = new SerialPortShim("COM4", 19200, Hardware.Parity.None, 8, Hardware.StopBits.One))
            {
                port.ReadTimeout = TimeSpan.FromSeconds(15);
                port.Open();

                byte address = 201;
                ushort setpointRegister = 40346;
                ushort tempRegister = 40122;

                var run = true;

                var client = new ModbusRtuClient(port);

                var reader = Task.Run(async () =>
                {
                    while (run)
                    {
                        Debug.WriteLine("<--");
                        var setpoint = await client.ReadHoldingRegisters(address, setpointRegister, 1);
                        var temp = await client.ReadHoldingRegisters(address, tempRegister, 1);
                        await Task.Delay(1000);
                    }
                });

                var writer = Task.Run(async () =>
                {
                    ushort sp = 600;

                    for(int i = 0; i < 10; i++)
                    {
                        Debug.WriteLine("-->");
                        await client.WriteHoldingRegister(address, setpointRegister, sp);
                        sp += 10;
                        await Task.Delay(700);
                    }

                    run = false;
                });


                while (run)
                {
                    await Task.Delay(1000);
                }
            }
        }

        [Fact]
        public async void ReadHoldingRegisterTest()
        {
            using (var port = new SerialPortShim("COM4", 19200, Hardware.Parity.None, 8, Hardware.StopBits.One))
            {
                port.ReadTimeout = 15000;
                port.Open();

                byte address = 201;
                ushort startRegister = 1;
                var readCount = 1;

                var client = new ModbusRtuClient(port);
                var r1 = await client.ReadHoldingRegisters(address, startRegister, readCount);
                Assert.Equal(readCount, r1.Length);

                readCount = 2;
                var r2 = await client.ReadHoldingRegisters(address, startRegister, readCount);
                Assert.Equal(readCount, r2.Length);

                Assert.Equal(r1[0], r2[0]);
            }
        }

        [Fact]
        public async void ReadWriteHoldingRegisterTest()
        {
            using (var port = new SerialPortShim("COM4", 19200, Hardware.Parity.None, 8, Hardware.StopBits.One))
            {
                port.ReadTimeout = 15000;
                port.Open();

                byte address = 201;
                ushort startRegister = 345; // occupied setpoint, in tenths of a degree
                var readCount = 1;

                var client = new ModbusRtuClient(port);
                var setpoint = await client.ReadHoldingRegisters(address, startRegister, readCount);

                // TODO: verify it's reasonable?

                // add or subtract some random amount
                var r = new Random();
                var delta = r.Next(-20, 20);
                var newSetpoint = (ushort)(setpoint[0] + delta);

                await client.WriteHoldingRegister(address, startRegister, newSetpoint);
                var verifySetpoint = await client.ReadHoldingRegisters(address, startRegister, readCount);

                Assert.Equal(newSetpoint, verifySetpoint[0]);
            }
        }
        */
    }
}