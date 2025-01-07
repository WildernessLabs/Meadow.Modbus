﻿using System;

namespace Meadow.Modbus.Temco;

public class TStat8 : ModbusPolledDevice
{
    private float _currentSetPoint;

    private const ushort SetPointRegister = 345;

    public TStat8(ModbusRtuClient client, byte modbusAddress, TimeSpan? refreshPeriod = null)
        : base(client, modbusAddress, refreshPeriod)
    {
        MapHoldingRegistersToProperty(
            startRegister: 121,
            registerCount: 1,
            propertyName: nameof(Temperature),
            scale: 0.10); // value is in 0.1 deg

        // map to a field, not a property as the property setter needs to perform an action
        MapHoldingRegistersToField(
            startRegister: SetPointRegister,
            registerCount: 1,
            fieldName: nameof(_currentSetPoint),
            scale: 0.10);

        MapHoldingRegistersToProperty(
            startRegister: 198,
            registerCount: 1,
            propertyName: nameof(Humidity));

        MapHoldingRegistersToProperty(
            startRegister: 364, // not scaled by 0.1
            registerCount: 1,
            propertyName: nameof(PowerUpSetPoint));

        MapHoldingRegistersToProperty(
            startRegister: 365,
            registerCount: 1,
            propertyName: nameof(MaxSetPoint));

        MapHoldingRegistersToProperty(
            startRegister: 366,
            registerCount: 1,
            propertyName: nameof(MinSetPoint));

        MapHoldingRegistersToProperty(
            startRegister: 410,
            registerCount: 7,
            propertyName: nameof(Clock),
            conversionFunction: ConvertRegistersToClockTime);
    }

    private object ConvertRegistersToClockTime(ushort[] data)
    {
        // data[2] is week, so ignore
        return new DateTime(data[0], data[1], data[3], data[4], data[5], data[6]);
    }

    public DateTime Clock { get; private set; }
    public int Humidity { get; private set; }
    public float Temperature { get; private set; }
    public float MinSetPoint { get; private set; }
    public float MaxSetPoint { get; private set; }
    public float PowerUpSetPoint { get; private set; }

    public float SetPoint
    {
        get => _currentSetPoint;
        set
        {
            _ = WriteHoldingRegister(SetPointRegister, (ushort)(value * 10));
        }
    }
}
