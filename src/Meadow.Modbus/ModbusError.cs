using System;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Meadow.Modbus.Unit.Tests")]

namespace Meadow.Modbus
{
    public class ModbusException : Exception
    {
        internal ModbusException(ModbusErrorCode errorCode, ModbusFunction function)
        {
            ErrorCode = errorCode;
            Function = function;
        }

        public ModbusErrorCode ErrorCode { get; private set; }
        public ModbusFunction Function { get; private set; }
    }
}
