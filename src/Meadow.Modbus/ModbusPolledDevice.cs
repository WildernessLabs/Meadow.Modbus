using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Meadow.Modbus;

public class TStat8 : ModbusPolledDevice
{
    private float _currentSetPoint;

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
            startRegister: 345,
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
            _ = WriteHoldingRegister(345, (ushort)(value * 10));
        }
    }
}

public abstract class ModbusPolledDevice
{
    private class RegisterMapping
    {
        public ushort StartRegister { get; set; }
        public int RegisterCount { get; set; }
        public FieldInfo? FieldInfo { get; set; }
        public PropertyInfo? PropertyInfo { get; set; }
        public double? Scale { get; set; }
        public double? Offset { get; set; }
        public Func<ushort[], object>? ConversionFunction { get; set; }
    }

    private const int MinimumPollDelayMs = 100;
    private readonly SemaphoreSlim _mapLock = new SemaphoreSlim(1, 1);

    public readonly TimeSpan DefaultRefreshPeriod = TimeSpan.FromSeconds(5);

    private ModbusClientBase _client;
    private byte _address;
    private Timer _timer;
    private int _refreshPeriosMs;

    private List<RegisterMapping> _mapping = new();

    public virtual void StartPolling()
    {
        _timer.Change(_refreshPeriosMs, -1);
    }

    public virtual void StopPolling()
    {
        _timer.Change(-1, -1);
    }

    public ModbusPolledDevice(ModbusClientBase client, byte modbusAddress)
    {
    }

    public ModbusPolledDevice(ModbusClientBase client, byte modbusAddress, TimeSpan? refreshPeriod = null)
    {
        _client = client;
        _address = modbusAddress;
        _refreshPeriosMs = (int)(refreshPeriod ?? DefaultRefreshPeriod).TotalMilliseconds;
        _timer = new Timer(RefreshTimerProc, null, -1, -1);
    }

    protected async Task WriteHoldingRegister(ushort startRegister, params ushort[] data)
    {
        if (data.Length == 1)
        {
            await _client.WriteHoldingRegister(_address, startRegister, data[0]);
        }

        await _client.WriteHoldingRegisters(_address, startRegister, data);
    }

    protected void MapHoldingRegistersToProperty(ushort startRegister, int registerCount, string propertyName, double? scale = null, double? offset = null)
    {
        _mapLock.Wait();
        try
        {
            var prop = this.GetType().GetProperty(propertyName);
            _mapping.Add(new RegisterMapping
            {
                PropertyInfo = prop,
                StartRegister = startRegister,
                RegisterCount = registerCount,
                Scale = scale,
                Offset = offset
            });
        }
        finally
        {
            _mapLock.Release();
        }
    }

    protected void MapHoldingRegistersToField(ushort startRegister, int registerCount, string fieldName, double? scale = null, double? offset = null)
    {
        _mapLock.Wait();
        try
        {
            var field = this.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                ?? throw new ArgumentException($"Field '{fieldName}' not found");

            _mapping.Add(new RegisterMapping
            {
                FieldInfo = field,
                StartRegister = startRegister,
                RegisterCount = registerCount,
                Scale = scale,
                Offset = offset
            });
        }
        finally
        {
            _mapLock.Release();
        }
    }

    protected void MapHoldingRegistersToProperty(ushort startRegister, int registerCount, string propertyName, Func<ushort[], object> conversionFunction)
    {
        _mapLock.Wait();
        try
        {
            var prop = this.GetType().GetProperty(propertyName);
            _mapping.Add(new RegisterMapping
            {
                PropertyInfo = prop,
                StartRegister = startRegister,
                RegisterCount = registerCount,
                ConversionFunction = conversionFunction
            });
        }
        finally
        {
            _mapLock.Release();
        }
    }

    private async void RefreshTimerProc(object _)
    {
        var start = Environment.TickCount;

        await _mapLock.WaitAsync();
        try
        {
            // TODO: add support for group reads (i.e. contiguously mapped registers)

            foreach (var r in _mapping)
            {
                // read registers
                ushort[] data;

                try
                {
                    data = await _client.ReadHoldingRegisters(_address, r.StartRegister, r.RegisterCount);
                }
                catch (TimeoutException)
                {
                    break;
                }

                if (data.Length == 0)
                {
                    // TODO: should we notify or log or something?
                    return;
                }

                if (r.PropertyInfo != null)
                {
                    UpdateProperty(data, r);
                }
                else if (r.FieldInfo != null)
                {
                    UpdateField(data, r);
                }
                else
                {
                    // no field or prop - should not be possible
                    throw new ArgumentException();
                }

            }
        }
        finally
        {
            _mapLock.Release();
        }

        // subtract execution time from desired period
        var et = Environment.TickCount - start;
        var delay = _refreshPeriosMs - et;
        _timer.Change(delay > MinimumPollDelayMs ? delay : MinimumPollDelayMs, -1);
    }

    private void UpdateProperty(ushort[] data, RegisterMapping mapping)
    {
        if (mapping.ConversionFunction != null)
        {
            var converted = mapping.ConversionFunction(data);
            mapping.PropertyInfo!.SetValue(this, converted);
        }
        else
        {
            if (
                mapping.PropertyInfo!.PropertyType == typeof(double) ||
                mapping.PropertyInfo!.PropertyType == typeof(float))
            {
                UpdateDoubleProperty(data, mapping);
            }
            else if (
                mapping.PropertyInfo!.PropertyType == typeof(byte) ||
                mapping.PropertyInfo!.PropertyType == typeof(short) ||
                mapping.PropertyInfo!.PropertyType == typeof(int) ||
                mapping.PropertyInfo!.PropertyType == typeof(long))
            {
                UpdateIntegerProperty(data, mapping);
            }
            else
            {
                throw new NotSupportedException();
            }
        }
    }

    private void UpdateField(ushort[] data, RegisterMapping mapping)
    {
        if (mapping.ConversionFunction != null)
        {
            var converted = mapping.ConversionFunction(data);
            mapping.FieldInfo!.SetValue(this, converted);
        }
        else
        {
            if (
                mapping.FieldInfo!.FieldType == typeof(double) ||
                mapping.FieldInfo!.FieldType == typeof(float))
            {
                UpdateDoubleField(data, mapping);
            }
            else if (
                mapping.FieldInfo!.FieldType == typeof(byte) ||
                mapping.FieldInfo!.FieldType == typeof(short) ||
                mapping.FieldInfo!.FieldType == typeof(int) ||
                mapping.FieldInfo!.FieldType == typeof(long))
            {
                UpdateIntegerField(data, mapping);
            }
            else
            {
                throw new NotSupportedException();
            }
        }
    }

    private void UpdateIntegerProperty(ushort[] data, RegisterMapping mapping)
    {
        // do scale/offset        
        long final = mapping.RegisterCount switch
        {
            1 => data[0],
            2 => data[0] << 16 | data[1],
            4 => data[0] << 48 | data[1] << 32 | data[2] << 16 | data[3],
            _ => throw new ArgumentException()
        };

        if (mapping.Scale != null)
        {
            final = (long)(final * mapping.Scale.Value);
        }
        if (mapping.Offset != null)
        {
            final = (long)(final + mapping.Offset.Value);
        }

        if (mapping.PropertyInfo!.PropertyType == typeof(byte))
        {
            mapping.PropertyInfo!.SetValue(this, Convert.ToByte(final));
        }
        else if (mapping.PropertyInfo!.PropertyType == typeof(short))
        {
            mapping.PropertyInfo!.SetValue(this, Convert.ToInt16(final));
        }
        else if (mapping.PropertyInfo!.PropertyType == typeof(int))
        {
            mapping.PropertyInfo!.SetValue(this, Convert.ToInt32(final));
        }
        else if (mapping.PropertyInfo!.PropertyType == typeof(long))
        {
            mapping.PropertyInfo!.SetValue(this, final);
        }
    }

    private void UpdateDoubleProperty(ushort[] data, RegisterMapping mapping)
    {
        // do scale/offset        
        double final = mapping.RegisterCount switch
        {
            1 => data[0],
            2 => data[0] << 16 | data[1],
            4 => data[0] << 48 | data[1] << 32 | data[2] << 16 | data[3],
            _ => throw new ArgumentException()
        };

        if (mapping.Scale != null)
        {
            final *= mapping.Scale.Value;
        }
        if (mapping.Offset != null)
        {
            final += mapping.Offset.Value;
        }

        if (mapping.PropertyInfo!.PropertyType == typeof(double))
        {
            mapping.PropertyInfo!.SetValue(this, final);
        }
        else if (mapping.PropertyInfo!.PropertyType == typeof(float))
        {
            mapping.PropertyInfo!.SetValue(this, Convert.ToSingle(final));
        }
    }

    private void UpdateIntegerField(ushort[] data, RegisterMapping mapping)
    {
        // do scale/offset        
        long final = mapping.RegisterCount switch
        {
            1 => data[0],
            2 => data[0] << 16 | data[1],
            4 => data[0] << 48 | data[1] << 32 | data[2] << 16 | data[3],
            _ => throw new ArgumentException()
        };

        if (mapping.Scale != null)
        {
            final = (long)(final * mapping.Scale.Value);
        }
        if (mapping.Offset != null)
        {
            final = (long)(final + mapping.Offset.Value);
        }

        if (mapping.FieldInfo!.FieldType == typeof(byte))
        {
            mapping.FieldInfo!.SetValue(this, Convert.ToByte(final));
        }
        else if (mapping.FieldInfo!.FieldType == typeof(short))
        {
            mapping.FieldInfo!.SetValue(this, Convert.ToInt16(final));
        }
        else if (mapping.FieldInfo!.FieldType == typeof(int))
        {
            mapping.FieldInfo!.SetValue(this, Convert.ToInt32(final));
        }
        else if (mapping.FieldInfo!.FieldType == typeof(long))
        {
            mapping.FieldInfo!.SetValue(this, final);
        }
    }

    private void UpdateDoubleField(ushort[] data, RegisterMapping mapping)
    {
        // do scale/offset        
        double final = mapping.RegisterCount switch
        {
            1 => data[0],
            2 => data[0] << 16 | data[1],
            4 => data[0] << 48 | data[1] << 32 | data[2] << 16 | data[3],
            _ => throw new ArgumentException()
        };

        if (mapping.Scale != null)
        {
            final *= mapping.Scale.Value;
        }
        if (mapping.Offset != null)
        {
            final += mapping.Offset.Value;
        }

        if (mapping.FieldInfo!.FieldType == typeof(double))
        {
            mapping.FieldInfo!.SetValue(this, final);
        }
        else if (mapping.FieldInfo!.FieldType == typeof(float))
        {
            mapping.FieldInfo!.SetValue(this, Convert.ToSingle(final));
        }
    }
}
