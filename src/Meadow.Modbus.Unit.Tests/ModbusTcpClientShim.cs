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

        protected override byte[] GenerateReadMessage(byte modbusAddress, ModbusFunction function, ushort startRegister, int registerCount)
        {
            LastGeneratedMessage = base.GenerateReadMessage(modbusAddress, function, startRegister, registerCount);
            return LastGeneratedMessage;
        }

        protected override byte[] GenerateWriteMessage(byte modbusAddress, ModbusFunction function, ushort register, byte[] data)
        {
            LastGeneratedMessage = base.GenerateWriteMessage(modbusAddress, function, register, data);
            return LastGeneratedMessage;
        }
    }
}