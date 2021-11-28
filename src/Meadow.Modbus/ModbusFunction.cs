namespace Meadow.Modbus
{
    public enum ModbusFunction : byte
    {
        ReadCoil = 1,
        ReadDiscrete = 2,
        ReadHoldingRegister = 3,
        ReadInputRegister = 4,
        WriteCoil = 5,
        WriteRegister = 6,
        WriteMultipleCoils = 15,
        WriteMultipleRegisters = 16,
        ReadWriteMultipleRegisters = 23
    }
}
