using System.Net;

namespace Meadow.Modbus.Unit.Tests
{
    internal class ModbusTcpClientShim : ModbusTcpClient
    {
        public byte[] LastGeneratedMessage { get; private set; }

        public ModbusTcpClientShim()
            : base(IPAddress.None)
        {

        }

        protected override byte[] GenerateMessage(byte modbusAddress, ModbusFunction function, ushort register, byte[] data)
        {
            LastGeneratedMessage = base.GenerateMessage(modbusAddress, function, register, data);
            return LastGeneratedMessage;
        }
    }
}