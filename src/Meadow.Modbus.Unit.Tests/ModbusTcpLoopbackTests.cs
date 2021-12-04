using System;
using System.Threading;
using Xunit;

namespace Meadow.Modbus.Unit.Tests
{
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
                    var result = await client.ReadHoldingRegisters(255, testRegisterAddress, r.Next(2, 20));

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

        /*
                [Fact]
                public async void ReadSingleCoilLoopbackTest()
                {
                    var testData = new bool[1];
                    ushort testRegisterAddress = 1;

                    using (var server = new ModbusTcpServer(502))
                    using (var client = new ModbusTcpClient("127.0.0.1"))
                    {
                        server.ReadCoilRequest += (address, length) =>
                        {
                            Assert.Equal(testRegisterAddress, address);
                            Assert.Equal(testData.Length, length);

                            /*
                            var results = new bool[length];
                            // turn on every other one as a simple pattern
                            for (int i = 0; i < length; i += 2)
                            {
                                results[i] = true;
                            }

                            return new ModbusReadResult(testData);
                        };

                        server.Start();

                        await client.Connect();

                        // set the test data, which the server will return
                        testData[0] = true;
                        // call the server
                        var result = await client.ReadCoils(255, testRegisterAddress, 1);
                        Assert.True(result[0]);

                        // set the test data, which the server will return
                        testData[0] = false;
                        // call the server
                        result = await client.ReadCoils(255, testRegisterAddress, 1);
                        Assert.False(result[0]);
                    }
                }

                [Fact]
                public async void ReadMultipleCoilsLoopbackTest()
                {
                    var testData = new bool[0];
                    ushort testRegisterAddress = 1;

                    using (var server = new ModbusTcpServer(502))
                    using (var client = new TcpMaster("127.0.0.1", 502))
                    {
                        server.ReadCoilRequest += (address, length) =>
                        {
                            Assert.Equal(testRegisterAddress, address);
                            Assert.Equal(testData.Length, length);

                            return new ModbusReadResult(testData);
                        };

                        server.Start();

                        await client.Connect();

                        var r = new Random();

                        // we'll do 5 tests of random length and random content data
                        for (int i = 0; i < 5; i++)
                        {
                            var len = r.Next(2, 32);
                            // set the test data, which the server will return
                            testData = new bool[len];
                            for (var b = 0; b < testData.Length; b++)
                            {
                                testData[b] = r.Next() % 2 == 0;
                            }

                            // call the server
                            var result = await client.ReadCoils(255, testRegisterAddress, (short)testData.Length);

                            Assert.Equal(testData.Length, result.Length);

                            for (var b = 0; b < testData.Length; b++)
                            {
                                Assert.Equal(testData[b], result[b]);
                            }
                        }
                    }
                }

                [Fact]
                public async void ReadMultipleDiscretesLoopbackTest()
                {
                    var testData = new bool[0];
                    ushort testRegisterAddress = 1;

                    using (var server = new ModbusTcpServer(502))
                    using (var client = new TcpMaster("127.0.0.1", 502))
                    {
                        server.ReadDiscreteRequest += (address, length) =>
                        {
                            Assert.Equal(testRegisterAddress, address);
                            Assert.Equal(testData.Length, length);

                            return new ModbusReadResult(testData);
                        };

                        server.Start();

                        await client.Connect();

                        var r = new Random();

                        // we'll do 5 tests of random length and random content data
                        for (int i = 0; i < 5; i++)
                        {
                            var len = r.Next(2, 32);
                            // set the test data, which the server will return
                            testData = new bool[len];
                            for (var b = 0; b < testData.Length; b++)
                            {
                                testData[b] = r.Next() % 2 == 0;
                            }

                            // call the server
                            var result = await client.ReadDiscreteInputs(255, testRegisterAddress, (short)testData.Length);

                            Assert.Equal(testData.Length, result.Length);

                            for (var b = 0; b < testData.Length; b++)
                            {
                                Assert.Equal(testData[b], result[b]);
                            }
                        }
                    }
                }

                [Fact]
                public async void WriteSingleCoilLoopbackTest()
                {
                    var testData = new bool[1];

                    var r = new Random();
                    ushort testRegisterAddress = (ushort)r.Next(128);

                    using (var server = new ModbusTcpServer(502))
                    using (var client = new TcpMaster("127.0.0.1", 502))
                    {
                        server.WriteCoilRequest += (address, data) =>
                        {
                            Assert.Equal(testRegisterAddress, address);
                            Assert.Single(data);
                            Assert.Equal(testData[0], data[0]);

                            return new ModbusWriteResult((short)data.Length);
                        };

                        server.Start();

                        await client.Connect();

                        testData[0] = true;
                        await client.WriteCoil(255, testRegisterAddress, testData[0]);
                        testData[0] = false;
                        await client.WriteCoil(255, testRegisterAddress, testData[0]);
                    }
                }

                [Fact]
                public async void WriteMultipleCoilsLoopbackTest()
                {
                    var are = new AutoResetEvent(false);

                    var testData = new bool[0];

                    var r = new Random();
                    ushort testRegisterAddress = (ushort)r.Next(128);

                    using (var server = new ModbusTcpServer(502))
                    using (var client = new TcpMaster("127.0.0.1", 502))
                    {
                        server.WriteCoilRequest += (address, data) =>
                        {
                            Assert.Equal(testRegisterAddress, address);
                            Assert.Equal(testData.Length, data.Length);

                            for (var b = 0; b < testData.Length; b++)
                            {
                                Assert.Equal(testData[b], data[b]);
                            }

                            are.Set();

                            return new ModbusWriteResult((short)data.Length);
                        };

                        server.Start();

                        await client.Connect();

                        // we'll do 5 tests of random length and random content data
                        for (int i = 0; i < 5; i++)
                        {
                            var len = r.Next(2, 32);

                            testData = new bool[len];
                            for (var b = 0; b < testData.Length; b++)
                            {
                                testData[b] = r.Next() % 2 == 0;
                            }

                            // call the server
                            await client.WriteCoils(255, testRegisterAddress, testData);

                            Assert.True(are.WaitOne(2000));

                            are.Reset();
                        }

                    }
                }

                [Fact]
                public async void ReadMultipleHoldingRegistersLoopbackTest()
                {
                    var testData = new ushort[0];
                    ushort testRegisterAddress = 1;

                    using (var server = new ModbusTcpServer(502))
                    using (var client = new TcpMaster("127.0.0.1", 502))
                    {
                        server.ReadHoldingRegisterRequest += (address, length) =>
                        {
                            Assert.Equal(testRegisterAddress, address);
                            Assert.Equal(testData.Length, length);

                            return new ModbusReadResult(testData);
                        };

                        server.Start();

                        await client.Connect();

                        var r = new Random();

                        // we'll do 5 tests of random length and random content data
                        for (int i = 0; i < 5; i++)
                        {
                            var len = r.Next(2, 32);
                            // set the test data, which the server will return
                            testData = new ushort[len];
                            for (var b = 0; b < testData.Length; b++)
                            {
                                testData[b] = (ushort)r.Next(ushort.MaxValue);
                            }

                            // call the server
                            var result = await client.ReadHoldingRegisters(255, testRegisterAddress, (short)testData.Length);

                            Assert.Equal(testData.Length, result.Length);

                            for (var b = 0; b < testData.Length; b++)
                            {
                                Assert.Equal(testData[b], result[b]);
                            }
                        }
                    }
                }

                [Fact]
                public async void ReadMultipleInputRegistersLoopbackTest()
                {
                    var testData = new ushort[0];
                    ushort testRegisterAddress = 1;

                    using (var server = new ModbusTcpServer(502))
                    using (var client = new TcpMaster("127.0.0.1", 502))
                    {
                        server.ReadInputRegisterRequest += (address, length) =>
                        {
                            Assert.Equal(testRegisterAddress, address);
                            Assert.Equal(testData.Length, length);

                            return new ModbusReadResult(testData);
                        };

                        server.Start();

                        await client.Connect();

                        var r = new Random();

                        // we'll do 5 tests of random length and random content data
                        for (int i = 0; i < 5; i++)
                        {
                            var len = r.Next(2, 32);
                            // set the test data, which the server will return
                            testData = new ushort[len];
                            for (var b = 0; b < testData.Length; b++)
                            {
                                testData[b] = (ushort)r.Next(ushort.MaxValue);
                            }

                            // call the server
                            var result = await client.ReadInputRegisters(255, testRegisterAddress, (short)testData.Length);

                            Assert.Equal(testData.Length, result.Length);

                            for (var b = 0; b < testData.Length; b++)
                            {
                                Assert.Equal(testData[b], result[b]);
                            }
                        }
                    }
                }

                [Fact]
                public async void WriteMultipleRegistersLoopbackTest()
                {
                    var testData = new ushort[1];
                    ushort testRegisterAddress = 1;

                    using (var server = new ModbusTcpServer(502))
                    using (var client = new TcpMaster("127.0.0.1", 502))
                    {
                        server.WriteRegisterRequest += (address, data) =>
                        {
                            Assert.Equal(testRegisterAddress, address);
                            Assert.Equal(testData.Length, data.Length);
                            for (int i = 0; i < testData.Length; i++)
                            {
                                Assert.Equal(testData[i], data[i]);
                            }
                            return new ModbusWriteResult((short)testData.Length);
                        };

                        server.Start();

                        await client.Connect();

                        var r = new Random();

                        for (int i = 0; i < 5; i++)
                        {
                            var len = 2;// r.Next(2, 32);
                            testData = new ushort[len];
                            for (int s = 0; s < testData.Length; s++)
                            {
                                testData[s] = (ushort)r.Next(ushort.MaxValue);
                            }
                            await client.WriteHoldingRegisters(255, testRegisterAddress, testData);
                        }
                    }
                }
        */
    }
}