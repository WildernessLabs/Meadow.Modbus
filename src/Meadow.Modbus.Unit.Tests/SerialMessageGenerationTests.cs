using Xunit;

namespace Meadow.Modbus.Unit.Tests
{
    public class SerialMessageGenerationTests
    {
        [Fact]
        public void TestFunction3Generation()
        {
            var port = new MockSerialPort();
            var client = new ModbusRtuClient(port);

            client.ReadHoldingRegisters(7, 11, 13);

            // valid output from a known-good RTU sender
            var expected = new byte[] { 0x07, 0x03, 0x00, 0x0b, 0x00, 0x0d, 0xf5, 0xab };

            Assert.Equal(expected, port.OutputBuffer);
        }

        [Fact]
        public void TestFunction6Generation()
        {
            var port = new MockSerialPort();
            var client = new ModbusRtuClient(port);

            client.WriteHoldingRegister(1, 7, 42);

            // valid output from a known-good RTU sender
            var expected = new byte[] { 0x01, 0x06, 0x00, 0x07, 0x00, 0x2a, 0xb9, 0xd4 };

            Assert.Equal(expected, port.OutputBuffer);
        }
    }
}