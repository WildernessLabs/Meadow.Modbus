using System;
using System.Threading;
using Xunit;

namespace Meadow.Modbus.Unit.Tests
{
    [CollectionDefinition("Sequential", DisableParallelization = true)]
    public class NonParallelCollectionDefinitionClass
    {
    }

    [Collection("Sequential")]
    public class ModbusTcpLoopbackTests
    {
        [Fact]
        public async void WriteSingleHoldingRegisterLoopbackTest()
        {
            var testData = new ushort[1];
            ushort testRegisterAddress = 0;
            bool callbackOccurred = false;

            using (var server = new ModbusTcpServer(502))
            using (var client = new ModbusTcpClient("127.0.0.1"))
            {
                server.WriteRegisterRequest += (address, data) =>
                {
                    Assert.Equal(testRegisterAddress, address);
                    Assert.Equal(testData.Length, data.Length);
                    Assert.Equal(testData[0], data[0]);
                    callbackOccurred = true;
                    return new ModbusReadResult(testData);
                };

                server.Start();

                await client.Connect();

                var r = new Random();

                // loop for 5 register writes, writing a random value to a random address
                // the event handler above checks the result
                for (int i = 0; i < 5; i++)
                {
                    testRegisterAddress = (ushort)r.Next(ushort.MaxValue);
                    testData[0] = (ushort)r.Next(ushort.MaxValue);
                    callbackOccurred = false;
                    await client.WriteHoldingRegister(255, testRegisterAddress, testData[0]);
                    Assert.True(callbackOccurred);
                }
            }
        }

        [Fact]
        public async void ReadSingleHoldingRegisterLoopbackTest()
        {
            var testData = new ushort[1];
            ushort testRegisterAddress = 1;
            bool callbackOccurred = false;
            var r = new Random();

            using (var server = new ModbusTcpServer(502))
            using (var client = new ModbusTcpClient("127.0.0.1"))
            {
                server.ReadHoldingRegisterRequest += (address, length) =>
                {
                    Assert.Equal(testRegisterAddress, address);
                    Assert.Equal(testData.Length, length);

                    testData[0] = (ushort)r.Next(ushort.MaxValue);
                    callbackOccurred = true;
                    return new ModbusReadResult(testData);
                };

                server.Start();

                await client.Connect();

                // do five single-register reads
                for (int i = 0; i < 5; i++)
                {
                    callbackOccurred = false;

                    testRegisterAddress = (ushort)r.Next(ushort.MaxValue);
                    var result = await client.ReadHoldingRegisters(255, testRegisterAddress, 1);
                    Assert.Equal(testData[0], result[0]);

                    Assert.True(callbackOccurred);
                }
            }
        }

        [Fact]
        public async void ReadMultipleHoldingRegistersLoopbackTest()
        {
            ushort[]? testData = null;
            ushort testRegisterAddress = 1;
            bool callbackOccurred = false;
            var r = new Random();

            using (var server = new ModbusTcpServer(502))
            using (var client = new ModbusTcpClient("127.0.0.1"))
            {
                server.ReadHoldingRegisterRequest += (address, length) =>
                {
                    Assert.Equal(testRegisterAddress, address);

                    // generate some new random data
                    testData = new ushort[length];
                    for (int i = 0;i < testData.Length;i++)
                    {
                        testData[i] = (ushort)r.Next(ushort.MaxValue);
                    }

                    callbackOccurred = true;
                    return new ModbusReadResult(testData);
                };

                server.Start();

                await client.Connect();

                // do 5 multi-register reads, each read from 2 - 20 registers
                for (int i = 0; i < 5; i++)
                {
                    callbackOccurred = false;

                    testRegisterAddress = (ushort)r.Next(ushort.MaxValue);
                    var result = await client.ReadHoldingRegisters(255, testRegisterAddress, r.Next(2, 21));

                    Assert.True(callbackOccurred);
                    Assert.NotNull(testData);
                    Assert.Equal(testData.Length, result.Length);

                    for(int reg = 0; reg < testData.Length; reg++)
                    {
                        Assert.Equal(testData[reg], result[reg]);
                    }
                }
            }
        }

        [Fact]
        public async void WriteSingleCoilLoopbackTest()
        {
            var testData = new bool[1];
            ushort testRegisterAddress = 0;
            bool callbackOccurred = false;

            using (var server = new ModbusTcpServer(502))
            using (var client = new ModbusTcpClient("127.0.0.1"))
            {
                server.WriteCoilRequest += (address, data) =>
                {
                    Assert.Equal(testRegisterAddress, address);
                    Assert.Equal(testData.Length, data.Length);
                    Assert.Equal(testData[0], data[0]);
                    callbackOccurred = true;
                    return new ModbusReadResult(testData);
                };

                server.Start();

                await client.Connect();

                var r = new Random();

                // loop for 5 register writes, writing a random value to a random address
                // the event handler above checks the result
                for (int i = 0; i < 5; i++)
                {
                    testRegisterAddress = (ushort)r.Next(ushort.MaxValue);
                    testData[0] = r.Next(0, 2) == 1;
                    callbackOccurred = false;
                    await client.WriteCoil(255, testRegisterAddress, testData[0]);
                    Assert.True(callbackOccurred);
                }
            }
        }

        [Fact]
        public async void ReadCoilsLoopbackTest()
        {
            bool[]? testData = null;
            ushort testRegisterAddress = 11;
            bool callbackOccurred = false;
            var r = new Random();

            using (var server = new ModbusTcpServer(502))
            using (var client = new ModbusTcpClient("127.0.0.1"))
            {
                server.ReadCoilRequest += (address, count) =>
                {
                    Assert.Equal(testRegisterAddress, address);

                    // generate some new random data
                    testData = new bool[count];
                    for (int i = 0; i < testData.Length; i++)
                    {
                        testData[i] = r.Next(0, 2) == 1;
                    }

                    callbackOccurred = true;
                    return new ModbusReadResult(testData);
                };

                server.Start();

                await client.Connect();

                // loop for 5 reads - reading 1-16 coils
                // the event handler above checks the result
                for (int i = 0; i < 5; i++)
                {
                    testRegisterAddress = (ushort)r.Next(ushort.MaxValue);
                    var result = await client.ReadCoils(255, testRegisterAddress, r.Next(1, 17));
                    Assert.True(callbackOccurred);
                    Assert.NotNull(testData);
                    Assert.NotNull(result);
                    Assert.Equal(testData.Length, result.Length);

                    for (int index = 0; index < testData.Length; index++)
                    {
                        Assert.Equal(testData[index], result[index]);
                    }
                }
            }
        }
    }
}