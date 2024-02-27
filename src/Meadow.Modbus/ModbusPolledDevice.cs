using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Meadow.Modbus;

/// <summary>
/// Represents an abstract base class for Modbus devices where register values are polled
/// </summary>
public abstract class ModbusPolledDevice
{
    /// <summary>
    /// Represents the possible formats of source registers
    /// </summary>
    public enum SourceFormat
    {
        /// <summary>
        /// Little-endian integer format.
        /// </summary>
        LittleEndianInteger,

        /// <summary>
        /// Big-endian integer format.
        /// </summary>
        BigEndianInteger,

        /// <summary>
        /// Little-endian IEEE 794 floating-point format.
        /// </summary>
        LittleEndianFloat,

        /// <summary>
        /// Big-endian IEEE 794 floating-point format.
        /// </summary>
        BigEndianFloat,
    }

    private class RegisterMapping
    {
        public ushort StartRegister { get; set; }
        public int RegisterCount { get; set; }
        public FieldInfo? FieldInfo { get; set; }
        public PropertyInfo? PropertyInfo { get; set; }
        public double? Scale { get; set; }
        public double? Offset { get; set; }
        public Func<ushort[], object>? ConversionFunction { get; set; }
        public SourceFormat SourceFormat { get; set; } = SourceFormat.LittleEndianInteger;
    }

    private const int MinimumPollDelayMs = 100;
    private readonly SemaphoreSlim _mapLock = new SemaphoreSlim(1, 1);

    /// <summary>
    /// Gets the default refresh period for polling.
    /// </summary>
    public static readonly TimeSpan DefaultRefreshPeriod = TimeSpan.FromSeconds(5);

    private ModbusClientBase _client;
    private byte _address;
    private Timer _timer;
    private int _refreshPeriosMs;

    private List<RegisterMapping> _mapping = new();

    /// <summary>
    /// Starts polling the Modbus device.
    /// </summary>
    public virtual void StartPolling()
    {
        _timer.Change(_refreshPeriosMs, -1);
    }

    /// <summary>
    /// Stops polling the Modbus device.
    /// </summary>
    public virtual void StopPolling()
    {
        _timer.Change(-1, -1);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ModbusPolledDevice"/> class.
    /// </summary>
    /// <param name="client">The Modbus client for communication.</param>
    /// <param name="modbusAddress">The Modbus address of the device.</param>
    /// <param name="refreshPeriod">The optional refresh period for polling.</param>
    public ModbusPolledDevice(ModbusClientBase client, byte modbusAddress, TimeSpan? refreshPeriod = null)
    {
        _client = client;
        _address = modbusAddress;
        _refreshPeriosMs = (int)(refreshPeriod ?? DefaultRefreshPeriod).TotalMilliseconds;
        _timer = new Timer(RefreshTimerProc, null, -1, -1);
    }

    /// <summary>
    /// Writes one or more values to the holding registers of the Modbus device.
    /// </summary>
    /// <param name="startRegister">The starting register address.</param>
    /// <param name="data">The values to be written to the holding registers.</param>
    /// <returns>A task representing the asynchronous write operation.</returns>
    protected async Task WriteHoldingRegister(ushort startRegister, params ushort[] data)
    {
        if (data.Length == 1)
        {
            await _client.WriteHoldingRegister(_address, startRegister, data[0]);
        }

        await _client.WriteHoldingRegisters(_address, startRegister, data);
    }

    /// <summary>
    /// Maps a range of holding registers to a property of the Modbus device.
    /// </summary>
    /// <param name="startRegister">The starting register address.</param>
    /// <param name="registerCount">The number of registers to map.</param>
    /// <param name="propertyName">The name of the property to map the registers to.</param>
    /// <param name="scale">The optional scale factor to apply to the register values.</param>
    /// <param name="offset">The optional offset to apply to the register values.</param>
    /// <param name="sourceFormat">The format of the source registers</param>
    protected void MapHoldingRegistersToProperty(
        ushort startRegister,
        int registerCount,
        string propertyName,
        double? scale = null,
        double? offset = null,
        SourceFormat sourceFormat = SourceFormat.LittleEndianInteger)
    {
        _mapLock.Wait();
        try
        {
            var prop = this.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                ?? throw new ArgumentException($"Property '{propertyName}' not found");

            _mapping.Add(new RegisterMapping
            {
                PropertyInfo = prop,
                StartRegister = startRegister,
                RegisterCount = registerCount,
                Scale = scale,
                Offset = offset,
                SourceFormat = sourceFormat
            });
        }
        finally
        {
            _mapLock.Release();
        }
    }

    /// <summary>
    /// Maps a range of holding registers to a field of the Modbus device.
    /// </summary>
    /// <param name="startRegister">The starting register address.</param>
    /// <param name="registerCount">The number of registers to map.</param>
    /// <param name="fieldName">The name of the field to map the registers to.</param>
    /// <param name="scale">The optional scale factor to apply to the register values.</param>
    /// <param name="offset">The optional offset to apply to the register values.</param>
    /// <param name="sourceFormat">The format of the source registers</param>
    protected void MapHoldingRegistersToField(
        ushort startRegister,
        int registerCount,
        string fieldName,
        double? scale = null,
        double? offset = null,
        SourceFormat sourceFormat = SourceFormat.LittleEndianInteger)
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
                Offset = offset,
                SourceFormat = sourceFormat
            });
        }
        finally
        {
            _mapLock.Release();
        }
    }

    /// <summary>
    /// Maps a range of holding registers to a property of the Modbus device.
    /// </summary>
    /// <param name="startRegister">The starting register address.</param>
    /// <param name="registerCount">The number of registers to map.</param>
    /// <param name="propertyName">The name of the property to map the registers to.</param>
    /// <param name="conversionFunction">The custom conversion function to transform raw register values to the property type.</param>
    protected void MapHoldingRegistersToProperty(ushort startRegister, int registerCount, string propertyName, Func<ushort[], object> conversionFunction)
    {
        _mapLock.Wait();
        try
        {
            var prop = this.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                ?? throw new ArgumentException($"Property '{propertyName}' not found");

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

    /// <summary>
    /// Maps a range of holding registers to a field of the Modbus device.
    /// </summary>
    /// <param name="startRegister">The starting register address.</param>
    /// <param name="registerCount">The number of registers to map.</param>
    /// <param name="fieldName">The name of the property to map the registers to.</param>
    /// <param name="conversionFunction">The custom conversion function to transform raw register values to the field type.</param>
    protected void MapHoldingRegistersToField(ushort startRegister, int registerCount, string fieldName, Func<ushort[], object> conversionFunction)
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

    /// <summary>
    /// Retrieves the value from the specified register array based on the provided data format.
    /// </summary>
    /// <param name="data">The array of ushort register values.</param>
    /// <param name="format">The source data format indicating how to interpret the values in the data array.</param>
    /// <returns>The extracted value in its raw form.</returns>
    protected object GetValueByFormat(ushort[] data, SourceFormat format)
    {
        Span<byte> bytes = MemoryMarshal.Cast<ushort, byte>(data);

        switch (format)
        {
            case SourceFormat.LittleEndianFloat:
                return data.Length switch
                {
                    1 => BitConverter.ToSingle(bytes[..2]),
                    2 => BitConverter.ToSingle(bytes[..4]),
                    4 => BitConverter.ToDouble(bytes[..8]),
                    _ => throw new ArgumentException()
                };
            case SourceFormat.BigEndianFloat:
                Span<byte> bebytes = bytes
                    .ToArray()
                    .Reverse()
                    .ToArray()
                    .AsSpan();
                return data.Length switch
                {
                    1 => BitConverter.ToSingle(bebytes[..2]),
                    2 => BitConverter.ToSingle(bebytes[..4]),
                    4 => BitConverter.ToDouble(bebytes[..8]),
                    _ => throw new ArgumentException()
                };
            case SourceFormat.BigEndianInteger:
                return data.Length switch
                {
                    1 => BinaryPrimitives.ReadInt16BigEndian(bytes[..2]),
                    2 => BinaryPrimitives.ReadInt32BigEndian(bytes[..4]),
                    4 => BinaryPrimitives.ReadInt64BigEndian(bytes[..8]),
                    _ => throw new ArgumentException()
                };
            default:
                return data.Length switch
                {
                    1 => BinaryPrimitives.ReadInt16LittleEndian(bytes[..2]),
                    2 => BinaryPrimitives.ReadInt32LittleEndian(bytes[..4]),
                    4 => BinaryPrimitives.ReadInt64LittleEndian(bytes[..8]),
                    _ => throw new ArgumentException()
                };
        }
    }

    private void UpdateIntegerProperty(ushort[] data, RegisterMapping mapping)
    {
        var final = Convert.ToInt64(GetValueByFormat(data, mapping.SourceFormat));

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
        var final = Convert.ToDouble(GetValueByFormat(data, mapping.SourceFormat));

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
        var final = Convert.ToInt64(GetValueByFormat(data, mapping.SourceFormat));

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
        var final = Convert.ToDouble(GetValueByFormat(data, mapping.SourceFormat));

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
