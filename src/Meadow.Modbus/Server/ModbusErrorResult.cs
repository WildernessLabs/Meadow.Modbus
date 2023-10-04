namespace Meadow.Modbus;

/// <summary>
/// Represents a Modbus error result.
/// </summary>
public sealed class ModbusErrorResult : IModbusResult
{
    /// <summary>
    /// Gets the Modbus error code associated with the result.
    /// </summary>
    public ModbusErrorCode ErrorCode { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ModbusErrorResult"/> class with the specified error code.
    /// </summary>
    /// <param name="errorCode">The Modbus error code.</param>
    public ModbusErrorResult(ModbusErrorCode errorCode)
    {
        ErrorCode = errorCode;
    }
}
