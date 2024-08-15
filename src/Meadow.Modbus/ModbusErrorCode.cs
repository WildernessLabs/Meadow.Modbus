namespace Meadow.Modbus;

/// <summary>
/// Enumeration of Modbus error codes.
/// </summary>
public enum ModbusErrorCode
{
    /// <summary>
    /// Illegal function error code.
    /// </summary>
    IllegalFunction = 1,

    /// <summary>
    /// Illegal data address error code.
    /// </summary>
    IllegalDataAddress = 2,

    /// <summary>
    /// Illegal data value error code.
    /// </summary>
    IllegalDataValue = 3,

    /// <summary>
    /// Slave device failure error code.
    /// </summary>
    SlaveDeviceFailure = 4,

    /// <summary>
    /// Acknowledgment error code.
    /// </summary>
    Ack = 5,

    /// <summary>
    /// Slave is busy error code.
    /// </summary>
    SlaveIsBusy = 6,

    /// <summary>
    /// Gate path unavailable error code.
    /// </summary>
    GatePathUnavailable = 10,

    /// <summary>
    /// Gateway Target Device Failed to Respond.
    /// </summary>
    GatewayTimeoutError = 11,

    /// <summary>
    /// Send failed error code.
    /// </summary>
    SendFailed = 100,

    /// <summary>
    /// Invalid offset error code.
    /// </summary>
    InvalidOffset = 128,

    /// <summary>
    /// Not connected error code.
    /// </summary>
    NotConnected = 253,

    /// <summary>
    /// Connection lost error code.
    /// </summary>
    ConnectionLost = 254,

    /// <summary>
    /// Timeout error code.
    /// </summary>
    Timeout = 255
}
