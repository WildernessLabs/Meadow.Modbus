using System;
using System.Collections.Generic;
using Xunit;

namespace Meadow.Modbus.Unit.Tests
{
    public class SerialMessageGenerationTests
    {
        [Fact]
        public async void TestFunction3Generation()
        {
            var port = new MockSerialPort();
            var client = new ModbusRtuClient(port);

            try
            {
                await client.ReadHoldingRegisters(7, 11, 13);
            }
            catch (TimeoutException)
            {
                // NOP - expected
            }

            // valid output from a known-good RTU sender
            var expected = new byte[] { 0x07, 0x03, 0x00, 0x0b, 0x00, 0x0d, 0xf5, 0xab };

            Assert.Equal(expected, port.OutputBuffer);
        }

        [Fact]
        public async void TestFunction6Generation()
        {
            var port = new MockSerialPort();
            var client = new ModbusRtuClient(port);
            try
            {
                await client.WriteHoldingRegister(1, 7, 42);
            }
            catch (TimeoutException)
            {
                // NOP - expected
            }

            // valid output from a known-good RTU sender
            var expected = new byte[] { 0x01, 0x06, 0x00, 0x07, 0x00, 0x2a, 0xb9, 0xd4 };

            Assert.Equal(expected, port.OutputBuffer);
        }

        [Fact]
        public async void TestFunction5Generation()
        {
            var port = new MockSerialPort();
            var client = new ModbusRtuClient(port);

            try
            {
                await client.WriteCoil(1, 7, true);
            }
            catch (TimeoutException)
            {
                // NOP - expected
            }

            // valid output from a known-good RTU sender
            var expected = new byte[] { 0x01, 0x05, 0x00, 0x07, 0xff, 0x00, 0x3d, 0xfb };

            Assert.Equal(expected, port.OutputBuffer);
        }

        [Fact]
        public async void TestFunction1Generation()
        {
            var port = new MockSerialPort();
            var client = new ModbusRtuClient(port);

            try
            {
                await client.ReadCoils(17, 13, 7);
            }
            catch (TimeoutException)
            {
                // NOP - expected
            }

            // valid output from a known-good RTU sender
            var expected = new byte[] { 0x11, 0x01, 0x00, 0x0d, 0x00, 0x07, 0xee, 0x9b };

            Assert.Equal(expected, port.OutputBuffer);
        }

        [Fact]
        public async void TestFunction7Generation()
        {
            var port = new MockSerialPort();
            var client = new ModbusRtuClient(port);

            try
            {
                await client.WriteMultipleCoils(17, 19, new List<bool> { true, false, true, false, true, false, true, false, true, false, true, false, true, false, true, false, true });
            }
            catch (TimeoutException)
            {
                // NOP - expected
            }

            // valid output from a known-good RTU sender
            var expected = new byte[] { 0x11, 0x0F, 0x00, 0x13, 0x00, 0x11, 0x03, 0x55, 0x55, 0x01, 0x51, 0xA1 };

            Assert.Equal(expected, port.OutputBuffer);
        }
    }
}