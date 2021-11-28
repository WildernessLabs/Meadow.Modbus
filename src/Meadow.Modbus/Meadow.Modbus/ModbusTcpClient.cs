using System;
using System.Net;

namespace Meadow.Modbus
{
    public class ModbusTcpClient : ModbusClientBase
    {
        private const int HEADER_DATA_OFFSET = 10;

        private IPAddress _destination;

        public ModbusTcpClient(IPAddress destination)
        {
            _destination = destination;
        }

        public override void Connect()
        {
            throw new NotImplementedException();
        }

        public override void Disconnect()
        {
            throw new NotImplementedException();
        }

        protected override byte[] GenerateMessage(byte modbusAddress, ModbusFunction function, ushort register, byte[] data)
        {
            // Modbus TCP has different headers based on read/write or readwrite
            switch (function)
            {
                case ModbusFunction.WriteRegister:
                case ModbusFunction.WriteCoil:
                case ModbusFunction.WriteMultipleCoils:
                case ModbusFunction.WriteMultipleRegisters:
                    return GenerateWriteMessage(modbusAddress, function, register, data);
                default:
                    throw new NotImplementedException();
            }
        }

        private byte[] GenerateWriteMessage(byte modbusAddress, ModbusFunction function, ushort register, byte[] data)
        {
            // single and multiples are slightly different
            var headerSize = function switch
            {
                ModbusFunction.WriteCoil => 10,
                ModbusFunction.WriteRegister => 10,
                ModbusFunction.WriteMultipleCoils => 13,
                ModbusFunction.WriteMultipleRegisters => 13,
                _ => throw new ArgumentException("function is not a write")
            };

            var registerCount = data.Length / 2;

            var message = new byte[headerSize + data.Length]; // header + data

            message[0] = 0; // these first 2 bytes can be an "ID" but it's unique to TCP and I've never seen it used, so omitting for now
            message[1] = modbusAddress;
            message[2] = 0;
            message[3] = 0;

            var size = (ushort)(5 + registerCount);
            message[4] = (byte)(size >> 8);
            message[5] = (byte)(size & 0xff);

            message[6] = modbusAddress;
            message[7] = (byte)function;

            message[8] = (byte)(register >> 8);
            message[9] = (byte)(register & 0xff);

            Array.Copy(data, 0, message, headerSize, data.Length);

            switch(function)
            {
                case ModbusFunction.WriteMultipleCoils:
                case ModbusFunction.WriteMultipleRegisters:
                    // TODO: this is an item count, and is almost certainly wrong right now
                    var itemCount = IPAddress.HostToNetworkOrder((short)data.Length);
                    data[10] = (byte)(itemCount >> 8);
                    data[11] = (byte)(itemCount & 0xff);
                    data[12] = (byte)(data.Length - 2);
                    break;
            }

            // Unlike RTU, Modbus TCP doesn't do a CRC - it relies on TCP/IP to do error checking

            return message;
        }

        protected override void DeliverMessage(byte[] message)
        {
            if(_destination.Equals(IPAddress.None))
            {
                // this is used in testing, nothing gets sent
                return;
            }

            throw new NotImplementedException();
        }
    }
}
