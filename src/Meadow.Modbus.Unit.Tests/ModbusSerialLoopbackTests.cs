using System;
using Xunit;

namespace Meadow.Modbus.Unit.Tests;

public class ModbusSerialLoopbackTests
{
    [Fact]
    public async void ReadSingleHoldingRegisterTest()
    {
        using var portA = new SerialPortShim("COM3", 19200, Hardware.Parity.None, 8, Hardware.StopBits.One);
        using var portB = new SerialPortShim("COM6", 19200, Hardware.Parity.None, 8, Hardware.StopBits.One);

        var client = new ModbusRtuClient(portA);
        var server = new ModbusRtuServer(portB);

        ushort testData = 0;
        var startRegister = (ushort)Random.Shared.Next(1, 10000);
        var count = Random.Shared.Next(5, 25);
        var testAddress = (byte)Random.Shared.Next(1, 254);
        ushort currentRegister = 0;

        server.ReadHoldingRegisterRequest += (byte modbusAddress, ushort startRegister, short length) =>
        {
            testData = (ushort)Random.Shared.Next(1, ushort.MaxValue);
            Assert.Equal(testAddress, modbusAddress);
            Assert.Equal(currentRegister, startRegister);
            return new ModbusReadResult(new ushort[] { testData });
        };

        server.Start();
        await client.Connect();

        for (currentRegister = startRegister; currentRegister < startRegister + count; currentRegister++)
        {
            var hr = await client.ReadHoldingRegisters(testAddress, currentRegister, 1);

            Assert.Single(hr);
            Assert.Equal(testData, hr[0]);
        }
    }

    [Fact]
    public async void ReadMultipleHoldingRegisterTest()
    {
        using var portA = new SerialPortShim("COM3", 19200, Hardware.Parity.None, 8, Hardware.StopBits.One);
        using var portB = new SerialPortShim("COM6", 19200, Hardware.Parity.None, 8, Hardware.StopBits.One);

        var client = new ModbusRtuClient(portA);
        var server = new ModbusRtuServer(portB);

        ushort[] testData = new ushort[0];
        var startRegister = (ushort)Random.Shared.Next(1, 10000);
        var count = Random.Shared.Next(5, 25);
        var testAddress = (byte)Random.Shared.Next(1, 254);
        ushort currentRegister = 0;

        server.ReadHoldingRegisterRequest += (byte modbusAddress, ushort startRegister, short length) =>
        {
            // create 
            testData = new ushort[length];
            for (var i = 0; i < testData.Length; i++)
            {
                testData[i] = (ushort)Random.Shared.Next(1, ushort.MaxValue);
            }

            Assert.Equal(testAddress, modbusAddress);
            Assert.Equal(currentRegister, startRegister);
            return new ModbusReadResult(testData);
        };

        server.Start();
        await client.Connect();

        for (currentRegister = startRegister; currentRegister < startRegister + count; currentRegister++)
        {
            var registerCount = Random.Shared.Next(2, 20);

            var hr = await client.ReadHoldingRegisters(testAddress, currentRegister, registerCount);

            Assert.Equal(registerCount, hr.Length);
            for (var i = 0; i < hr.Length; i++)
            {
                Assert.Equal(testData[i], hr[i]);
            }
        }
    }

    [Fact]
    public async void ReadSingleInputRegisterTest()
    {
        using var portA = new SerialPortShim("COM3", 19200, Hardware.Parity.None, 8, Hardware.StopBits.One);
        using var portB = new SerialPortShim("COM6", 19200, Hardware.Parity.None, 8, Hardware.StopBits.One);

        var client = new ModbusRtuClient(portA);
        var server = new ModbusRtuServer(portB);

        ushort testData = 0;
        var startRegister = (ushort)Random.Shared.Next(1, 10000);
        var count = Random.Shared.Next(5, 25);
        var testAddress = (byte)Random.Shared.Next(1, 254);
        ushort currentRegister = 0;

        server.ReadInputRegisterRequest += (byte modbusAddress, ushort startRegister, short length) =>
        {
            testData = (ushort)Random.Shared.Next(1, ushort.MaxValue);
            Assert.Equal(testAddress, modbusAddress);
            Assert.Equal(currentRegister, startRegister);
            return new ModbusReadResult(new ushort[] { testData });
        };

        server.Start();
        await client.Connect();

        for (currentRegister = startRegister; currentRegister < startRegister + count; currentRegister++)
        {
            var hr = await client.ReadInputRegisters(testAddress, currentRegister, 1);

            Assert.Single(hr);
            Assert.Equal(testData, hr[0]);
        }
    }

    [Fact]
    public async void ReadMultipleInputRegisterTest()
    {
        using var portA = new SerialPortShim("COM3", 19200, Hardware.Parity.None, 8, Hardware.StopBits.One);
        using var portB = new SerialPortShim("COM6", 19200, Hardware.Parity.None, 8, Hardware.StopBits.One);

        var client = new ModbusRtuClient(portA);
        var server = new ModbusRtuServer(portB);

        ushort[] testData = new ushort[0];
        var startRegister = (ushort)Random.Shared.Next(1, 10000);
        var count = Random.Shared.Next(5, 25);
        var testAddress = (byte)Random.Shared.Next(1, 254);
        ushort currentRegister = 0;

        server.ReadInputRegisterRequest += (byte modbusAddress, ushort startRegister, short length) =>
        {
            // create 
            testData = new ushort[length];
            for (var i = 0; i < testData.Length; i++)
            {
                testData[i] = (ushort)Random.Shared.Next(1, ushort.MaxValue);
            }

            Assert.Equal(testAddress, modbusAddress);
            Assert.Equal(currentRegister, startRegister);
            return new ModbusReadResult(testData);
        };

        server.Start();
        await client.Connect();

        for (currentRegister = startRegister; currentRegister < startRegister + count; currentRegister++)
        {
            var registerCount = Random.Shared.Next(2, 20);

            var hr = await client.ReadInputRegisters(testAddress, currentRegister, registerCount);

            Assert.Equal(registerCount, hr.Length);
            for (var i = 0; i < hr.Length; i++)
            {
                Assert.Equal(testData[i], hr[i]);
            }
        }
    }

    [Fact]
    public async void WriteSingleHoldingRegisterTest()
    {
        using var portA = new SerialPortShim("COM3", 19200, Hardware.Parity.None, 8, Hardware.StopBits.One);
        using var portB = new SerialPortShim("COM6", 19200, Hardware.Parity.None, 8, Hardware.StopBits.One);

        var client = new ModbusRtuClient(portA);
        var server = new ModbusRtuServer(portB);

        ushort testData = 0;
        var startRegister = (ushort)Random.Shared.Next(1, 10000);
        var count = Random.Shared.Next(5, 25);
        var testAddress = (byte)Random.Shared.Next(1, 254);
        ushort currentRegister = 0;

        server.WriteRegisterRequest += (byte modbusAddress, ushort startRegister, ushort[] data) =>
        {
            Assert.Single(data);
            Assert.Equal(testData, data[0]);

            return new ModbusWriteResult((short)data.Length);
        };

        server.Start();
        await client.Connect();

        for (currentRegister = startRegister; currentRegister < startRegister + count; currentRegister++)
        {
            testData = (ushort)Random.Shared.Next(1, ushort.MaxValue);

            await client.WriteHoldingRegister(testAddress, currentRegister, testData);
        }
    }

}
