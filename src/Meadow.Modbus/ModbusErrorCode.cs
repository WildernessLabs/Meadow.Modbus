namespace Meadow.Modbus
{
    public enum ModbusErrorCode
    {
        IllegalFunction = 1,
        IllegalDataAddress = 2,
        IllegalDataValue = 3,
        SlaveDeviceFailure = 4,
        Ack = 5,
        SlaveIsBusy = 6,
        GatePathUnavailable = 10,
        SendFailed = 100,
        InvalidOffset = 128,
        NotConnected = 253,
        ConnectionLost = 254,
        Timeout = 255
    }
}
