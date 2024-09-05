using Meadow.Units;
using System;

namespace Meadow.Modbus.Voltaic;

public class V10x : ModbusPolledDevice
{
    private double _rawBatteryVoltage;
    private double _rawInputVoltage;
    private double _rawInputCurrent;
    private double _rawLoadVoltage;
    private double _rawLoadCurrent;
    private double _rawEnvironmentTemp;
    private double _rawControllerTemp;

    public const int DefaultModbusAddress = 1;
    public const int DefaultBaudRate = 9600;

    private const ushort BatteryOutputSwitchRegister = 0;

    public Voltage BatteryVoltage => new Voltage(_rawBatteryVoltage, Voltage.UnitType.Volts);
    public Voltage InputVoltage => new Voltage(_rawInputVoltage, Voltage.UnitType.Volts);
    public Current InputCurrent => new Current(_rawInputCurrent, Current.UnitType.Amps);
    public Voltage LoadVoltage => new Voltage(_rawLoadVoltage, Voltage.UnitType.Volts);
    public Current LoadCurrent => new Current(_rawLoadCurrent, Current.UnitType.Amps);
    public Temperature EnvironmentTemp => new Temperature(_rawEnvironmentTemp, Temperature.UnitType.Celsius);
    public Temperature ControllerTemp => new Temperature(_rawControllerTemp, Temperature.UnitType.Celsius);

    public V10x(
        ModbusClientBase client,
        byte modbusAddress = DefaultModbusAddress,
        TimeSpan? refreshPeriod = null)
        : base(client, modbusAddress, refreshPeriod)
    {
        MapInputRegistersToField(
            startRegister: 0x30a0,
            registerCount: 1,
            fieldName: nameof(_rawBatteryVoltage),
            conversionFunction: ConvertRegisterToRawValue
            );

        MapInputRegistersToField(
            startRegister: 0x304e,
            registerCount: 1,
            fieldName: nameof(_rawInputVoltage),
            conversionFunction: ConvertRegisterToRawValue
            );

        MapInputRegistersToField(
            startRegister: 0x304f,
            registerCount: 1,
            fieldName: nameof(_rawInputCurrent),
            conversionFunction: ConvertRegisterToRawValue
            );

        MapInputRegistersToField(
            startRegister: 0x304a,
            registerCount: 1,
            fieldName: nameof(_rawLoadVoltage),
            conversionFunction: ConvertRegisterToRawValue
            );

        MapInputRegistersToField(
            startRegister: 0x304b,
            registerCount: 1,
            fieldName: nameof(_rawLoadCurrent),
            conversionFunction: ConvertRegisterToRawValue
            );

        MapInputRegistersToField(
            startRegister: 0x30a2,
            registerCount: 1,
            fieldName: nameof(_rawEnvironmentTemp),
            conversionFunction: ConvertRegisterToRawValue
            );

        MapInputRegistersToField(
            startRegister: 0x3037,
            registerCount: 1,
            fieldName: nameof(_rawControllerTemp),
            conversionFunction: ConvertRegisterToRawValue
            );
    }

    public bool BatteryOutput
    {
        set => _ = WriteCoil(BatteryOutputSwitchRegister, value);
    }

    private object ConvertRegisterToRawValue(ushort[] registers)
    {
        // value is one register in 1/100 of a unit
        return registers[0] / 100d;
    }
}
