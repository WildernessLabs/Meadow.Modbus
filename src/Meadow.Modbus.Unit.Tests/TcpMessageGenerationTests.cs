using System;
using Xunit;

namespace Meadow.Modbus.Unit.Tests
{
    public class TcpMessageGenerationTests
    {
        [Fact]
        public async void TestFunction3Generation()
        {
            var client = new ModbusTcpClientShim();

            await client.ReadHoldingRegisters(7, 11, 13);

            // valid output from a known-good RTU sender
            var expected = new byte[] { 0x00, 0x01, 0x00, 0x00, 0x00, 0x06, 0x07, 0x03, 0x00, 0x0b, 0x00, 0x0d };

            Assert.Equal(expected, client.LastGeneratedMessage);
        }

        [Fact]
        public async void TestFunction6Generation()
        {
            var client = new ModbusTcpClientShim();

            await client.WriteHoldingRegister(1, 7, 42);

            // valid output from a known-good RTU sender
            var expected = new byte[] { 0x00, 0x01, 0x00, 0x00, 0x00, 0x06, 0x01, 0x06, 0x00, 0x07, 0x00, 0x2a };

            Assert.Equal(expected, client.LastGeneratedMessage);
        }

        [Fact]
        public async void TestFunction5Generation()
        {
            var client = new ModbusTcpClientShim();

            await client.WriteCoil(1, 7, true);

            // valid output from a known-good RTU sender
            var expected = new byte[] { 0x00, 0x01, 0x00, 0x00, 0x00, 0x06, 0x01, 0x05, 0x00, 0x07, 0xff, 0xff };

            Assert.Equal(expected, client.LastGeneratedMessage);
        }

        [Fact]
        public async void TestFunction1Generation()
        {
            var client = new ModbusTcpClientShim();

            await client.ReadCoils(17, 13, 7);

            // valid output from a known-good RTU sender
            var expected = new byte[] { 0x00, 0x01, 0x00, 0x00, 0x00, 0x06, 0x11, 0x01, 0x00, 0x0d, 0x00, 0x07 };

            Assert.Equal(expected, client.LastGeneratedMessage);
        }
    }
}