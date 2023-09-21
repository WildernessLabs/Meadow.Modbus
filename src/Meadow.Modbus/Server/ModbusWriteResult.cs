namespace Meadow.Modbus;

/// <summary>
/// Represents a Modbus write result.
/// </summary>
public sealed class ModbusWriteResult : IModbusResult
{
    /// <summary>
    /// Gets the number of items written in the write operation.
    /// </summary>
    public short ItemsWritten { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ModbusWriteResult"/> class with the specified number of items written.
    /// </summary>
    /// <param name="itemsWritten">The number of items written.</param>
    public ModbusWriteResult(short itemsWritten)
    {
        ItemsWritten = itemsWritten;
    }
}
