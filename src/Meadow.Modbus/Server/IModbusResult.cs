using System;
using System.Net;

namespace Meadow.Modbus
{
    public interface IModbusResult { }

    internal struct ModbusTcpHeader
    {
        public static int Length = 7;

        public ModbusTcpHeader(byte[] data, int offset)
        {
            var index = offset;
            TransactionID = IPAddress.NetworkToHostOrder(BitConverter.ToInt16(data, index));
            index += 2;
            ProtocolID = IPAddress.NetworkToHostOrder(BitConverter.ToInt16(data, index));
            index += 2;
            DataLength = IPAddress.NetworkToHostOrder(BitConverter.ToInt16(data, index));
            index += 2;
            UnitID = data[index];
        }

        public short TransactionID;
        public short ProtocolID; // 0 == modbus
        public short DataLength;
        public byte UnitID;
    }

    public sealed class ModbusErrorResult : IModbusResult
    {
        public ModbusErrorCode ErrorCode { get; }

        public ModbusErrorResult(ModbusErrorCode errorCode)
        {
            ErrorCode = errorCode;
        }
    }
}
