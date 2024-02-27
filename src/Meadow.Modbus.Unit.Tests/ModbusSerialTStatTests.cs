using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Xunit;

namespace Meadow.Modbus.Unit.Tests;

public class TStat8 : ModbusPolledDevice
{
    private float _currentSetPoint;

    private const ushort SetPointRegister = 345;

    public TStat8(ModbusRtuClient client, byte modbusAddress, TimeSpan? refreshPeriod = null)
        : base(client, modbusAddress, refreshPeriod)
    {
        MapHoldingRegistersToProperty(
            startRegister: 121,
            registerCount: 1,
            propertyName: nameof(Temperature),
            scale: 0.10); // value is in 0.1 deg

        // map to a field, not a property as the property setter needs to perform an action
        MapHoldingRegistersToField(
            startRegister: SetPointRegister,
            registerCount: 1,
            fieldName: nameof(_currentSetPoint),
            scale: 0.10);

        MapHoldingRegistersToProperty(
            startRegister: 198,
            registerCount: 1,
            propertyName: nameof(Humidity));

        MapHoldingRegistersToProperty(
            startRegister: 364, // not scaled by 0.1
            registerCount: 1,
            propertyName: nameof(PowerUpSetPoint));

        MapHoldingRegistersToProperty(
            startRegister: 365,
            registerCount: 1,
            propertyName: nameof(MaxSetPoint));

        MapHoldingRegistersToProperty(
            startRegister: 366,
            registerCount: 1,
            propertyName: nameof(MinSetPoint));

        MapHoldingRegistersToProperty(
            startRegister: 410,
            registerCount: 7,
            propertyName: nameof(Clock),
            conversionFunction: ConvertRegistersToClockTime);
    }

    private object ConvertRegistersToClockTime(ushort[] data)
    {
        // data[2] is week, so ignore
        return new DateTime(data[0], data[1], data[3], data[4], data[5], data[6]);
    }

    public DateTime Clock { get; private set; }
    public int Humidity { get; private set; }
    public float Temperature { get; private set; }
    public float MinSetPoint { get; private set; }
    public float MaxSetPoint { get; private set; }
    public float PowerUpSetPoint { get; private set; }

    public float SetPoint
    {
        get => _currentSetPoint;
        set
        {
            _ = WriteHoldingRegister(SetPointRegister, (ushort)(value * 10));
        }
    }
}

public class ModbusSerialTStatTests
{
    // this class assumes a connected serial Temco Controls TSTAT7 or TSTAT8
    [Fact]
    public async void PolledDevicetest()
    {
        using (var port = new SerialPortShim("COM3", 19200, Hardware.Parity.None, 8, Hardware.StopBits.One))
        {
            port.ReadTimeout = TimeSpan.FromSeconds(15);
            port.Open();

            var client = new ModbusRtuClient(port);
            var tstat = new TStat8(client, 201, TimeSpan.FromSeconds(1));
            tstat.StartPolling();

            var i = 0;
            while (true)
            {
                await Task.Delay(1000);
                Debug.WriteLine($"Temp: {tstat.Temperature}");
                Debug.WriteLine($"SetPoint: {tstat.SetPoint}");
                Debug.WriteLine($"MinSetPoint: {tstat.MinSetPoint}");
                Debug.WriteLine($"MaxSetPoint: {tstat.MaxSetPoint}");
                Debug.WriteLine($"PowerUpSetPoint: {tstat.PowerUpSetPoint}");
                Debug.WriteLine($"Humidity: {tstat.Humidity}");
                Debug.WriteLine($"Clock: {tstat.Clock}");
            }
        }
    }

    // this class assumes a connected serial Temco Controls TSTAT7 or TSTAT8
    [Fact(Skip = "Requires a connected TSTAT8 over RS485")]
    public async void MultipleReadHoldingRegisterTest()
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
    public async void MultipleWriteHoldingRegisterTest()
    {
        using (var port = new SerialPortShim("COM3", 19200, Hardware.Parity.None, 8, Hardware.StopBits.One))
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

    [Fact(Skip = "Requires a connected TSTAT8 over RS485")]
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

                for (int i = 0; i < 10; i++)
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

    [Fact(Skip = "Requires a connected TSTAT8 over RS485")]
    public async void ReadHoldingRegisterTest()
    {
        using (var port = new SerialPortShim("COM4", 19200, Hardware.Parity.None, 8, Hardware.StopBits.One))
        {
            port.ReadTimeout = TimeSpan.FromSeconds(15);
            port.Open();

            byte address = 201;
            ushort startRegister = 1;
            var readCount = 1;

            var client = new ModbusRtuClient(port);
            {
                // force meadow to compile the serial stuff
            };

            var r1 = await client.ReadHoldingRegisters(address, startRegister, readCount);
            Assert.Equal(readCount, r1.Length);

            readCount = 2;
            var r2 = await client.ReadHoldingRegisters(address, startRegister, readCount);
            Assert.Equal(readCount, r2.Length);

            Assert.Equal(r1[0], r2[0]);
        }
    }

    [Fact(Skip = "Requires a connected TSTAT8 over RS485")]
    public async void ReadWriteHoldingRegisterTest()
    {
        using (var port = new SerialPortShim("COM4", 19200, Hardware.Parity.None, 8, Hardware.StopBits.One))
        {
            port.ReadTimeout = TimeSpan.FromSeconds(15);
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
}