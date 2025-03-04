namespace Meadow.Modbus;

/// <summary>
/// Enumeration of Modbus function codes.
/// </summary>
public enum ModbusFunction : byte
{
    /// <summary>
    /// Modbus function code for reading coils (1).
    /// </summary>
    ReadCoil = 1,

    /// <summary>
    /// Modbus function code for reading discrete inputs (2).
    /// </summary>
    ReadDiscrete = 2,

    /// <summary>
    /// Modbus function code for reading holding registers (3).
    /// </summary>
    ReadHoldingRegister = 3,

    /// <summary>
    /// Modbus function code for reading input registers (4).
    /// </summary>
    ReadInputRegister = 4,

    /// <summary>
    /// Modbus function code for writing a single coil (5).
    /// </summary>
    WriteCoil = 5,

    /// <summary>
    /// Modbus function code for writing a single register (6).
    /// </summary>
    WriteRegister = 6,

    /// <summary>
    /// Modbus function code for writing multiple coils (15).
    /// </summary>
    WriteMultipleCoils = 15,

    /// <summary>
    /// Modbus function code for writing multiple registers (16).
    /// </summary>
    WriteMultipleRegisters = 16,

    /// <summary>
    /// Modbus function code for reading a device ID (17).
    /// </summary>
    ReportId = 17,

    /// <summary>
    /// Modbus function code for reading and writing multiple registers (23).
    /// </summary>
    ReadWriteMultipleRegisters = 23
}
