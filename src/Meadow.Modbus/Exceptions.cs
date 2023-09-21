using System;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Meadow.Modbus.Unit.Tests")]

namespace Meadow.Modbus;

/// <summary>
/// Exception thrown when a CRC (Cyclic Redundancy Check) failure occurs.
/// </summary>
public class CrcException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CrcException"/> class with a default error message.
    /// </summary>
    internal CrcException()
        : base("CRC Failure")
    {
    }
}

/// <summary>
/// Exception thrown when a Modbus operation encounters an error.
/// </summary>
public class ModbusException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ModbusException"/> class with the specified error code and function.
    /// </summary>
    /// <param name="errorCode">The Modbus error code associated with the exception.</param>
    /// <param name="function">The Modbus function associated with the exception.</param>
    internal ModbusException(ModbusErrorCode errorCode, ModbusFunction function)
    {
        ErrorCode = errorCode;
        Function = function;
    }

    /// <summary>
    /// Gets the Modbus error code associated with the exception.
    /// </summary>
    public ModbusErrorCode ErrorCode { get; private set; }

    /// <summary>
    /// Gets the Modbus function associated with the exception.
    /// </summary>
    public ModbusFunction Function { get; private set; }
}

