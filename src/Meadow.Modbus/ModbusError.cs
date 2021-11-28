using System;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Meadow.Modbus.Unit.Tests")]

namespace Meadow.Modbus
{
    public class ModbusException : Exception
    {
        public ModbusException(ModbusErrorCode errorCode, byte function)
        {
            ErrorCode = errorCode;
            Function = function;
        }

        public ModbusErrorCode ErrorCode { get; private set; }
        public byte Function { get; private set; }
    }
}
