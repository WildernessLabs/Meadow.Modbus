using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Meadow.Modbus;

/// <summary>
/// Base class for a Modbus client.
/// </summary>
public abstract class ModbusClientBase : IModbusBusClient, IDisposable
{
    private const int MaxRegisterReadCount = 125;

    /// <summary>
    /// Event triggered when the client is disconnected.
    /// </summary>
    public event EventHandler Disconnected = delegate { };

    /// <summary>
    /// Event triggered when the client is connected.
    /// </summary>
    public event EventHandler Connected = delegate { };

    private bool _connected;
    private readonly SemaphoreSlim _syncRoot = new SemaphoreSlim(1, 1);

    /// <summary>
    /// Gets a value indicating whether the client is disposed.
    /// </summary>
    public bool IsDisposed { get; private set; }

    /// <summary>
    /// Generates the message for writing data to a Modbus device.
    /// </summary>
    protected abstract byte[] GenerateWriteMessage(byte modbusAddress, ModbusFunction function, ushort register, byte[] data);

    /// <summary>
    /// Generates the message for reading data from a Modbus device.
    /// </summary>
    protected abstract byte[] GenerateReadMessage(byte modbusAddress, ModbusFunction function, ushort startRegister, int registerCount);

    /// <summary>
    /// Delivers the Modbus message to the device.
    /// </summary>
    protected abstract Task DeliverMessage(byte[] message);

    /// <summary>
    /// Reads the result of the Modbus function.
    /// </summary>
    protected abstract Task<byte[]> ReadResult(ModbusFunction function);

    /// <summary>
    /// Connects to the Modbus device.
    /// </summary>
    public abstract Task Connect();

    /// <summary>
    /// Disconnects from the Modbus device.
    /// </summary>
    public abstract void Disconnect();

    /// <summary>
    /// Releases the managed resources used by the Modbus client.
    /// </summary>
    protected virtual void DisposeManagedResources() { }

    /// <summary>
    /// Releases the unmanaged resources used by the Modbus client.
    /// </summary>
    protected virtual void DisposeUnmanagedResources() { }

    /// <summary>
    /// Disposes of the Modbus client.
    /// </summary>
    /// <param name="disposing">True if called from user code; false if called from the finalizer.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            if (disposing)
            {
                DisposeManagedResources();
            }

            DisposeUnmanagedResources();

            IsDisposed = true;
        }
    }

    /// <summary>
    /// Disposes of the Modbus client.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the client is connected to the Modbus device.
    /// </summary>
    public virtual bool IsConnected
    {
        get => _connected;
        protected set
        {
            _connected = value;

            if (_connected)
            {
                Connected?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                Disconnected?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>
    /// Writes a single value to a holding register on the Modbus device.
    /// </summary>
    /// <param name="modbusAddress">The Modbus device address.</param>
    /// <param name="register">The register number to write to.</param>
    /// <param name="value">The value to write to the register.</param>
    public async Task WriteHoldingRegister(byte modbusAddress, ushort register, ushort value)
    {
        if (register > 40000)
        {
            // holding registers are defined as starting at 40001, but the actual bus read doesn't use the address, but instead the offset
            // we'll support th user passing in the definition either way
            register -= 40001;
        }

        // swap endianness, because Modbus
        var data = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)value));
        var message = GenerateWriteMessage(modbusAddress, ModbusFunction.WriteRegister, register, data);
        await _syncRoot.WaitAsync();

        try
        {
            await DeliverMessage(message);
            await ReadResult(ModbusFunction.WriteRegister);
        }
        finally
        {
            _syncRoot.Release();
        }
    }

    /// <summary>
    /// Writes multiple values to consecutive holding registers on the Modbus device.
    /// </summary>
    /// <param name="modbusAddress">The Modbus device address.</param>
    /// <param name="startRegister">The starting register number to write to.</param>
    /// <param name="values">The values to write to the registers.</param>
    public async Task WriteHoldingRegisters(byte modbusAddress, ushort startRegister, IEnumerable<ushort> values)
    {
        if (startRegister > 40000)
        {
            // holding registers are defined as starting at 40001, but the actual bus read doesn't use the address, but instead the offset
            // we'll support th user passing in the definition either way
            startRegister -= 40001;
        }

        if (values.Count() == 0)
        {
            throw new ArgumentOutOfRangeException("Parameter 'values' contains no data");
        }

        // swap endianness, because Modbus
        var data = values.SelectMany(i => BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)i))).ToArray();

        var message = GenerateWriteMessage(modbusAddress, ModbusFunction.WriteMultipleRegisters, startRegister, data);
        await _syncRoot.WaitAsync();

        try
        {
            await DeliverMessage(message);
            await ReadResult(ModbusFunction.WriteMultipleRegisters);
        }
        finally
        {
            _syncRoot.Release();
        }
    }

    /// <summary>
    /// Reads floating point values from holding registers on the Modbus device.
    /// </summary>
    /// <param name="modbusAddress">The Modbus device address.</param>
    /// <param name="startRegister">The starting register number to read from.</param>
    /// <param name="floatCount">The number of floating point values to read.</param>
    /// <returns>An array of floating point values.</returns>
    public async Task<float[]> ReadHoldingRegistersFloat(byte modbusAddress, ushort startRegister, int floatCount)
    {
        var data = await ReadHoldingRegisters(modbusAddress, startRegister, floatCount * 2);

        var values = new float[data.Length / 2];

        for (int i = 0; i < values.Length; i++)
        {
            values[i] = ConvertUShortsToFloat(data[i * 2 + 1], data[i * 2]);
        }
        return values;
    }

    /// <summary>
    /// Reads holding registers from the Modbus device.
    /// </summary>
    /// <param name="modbusAddress">The Modbus device address.</param>
    /// <param name="startRegister">The starting register number to read from.</param>
    /// <param name="registerCount">The number of registers to read.</param>
    /// <returns>An array of ushort values representing the registers.</returns>
    public async Task<ushort[]> ReadHoldingRegisters(byte modbusAddress, ushort startRegister, int registerCount)
    {
        if (startRegister > 40000)
        {
            // holding registers are defined as starting at 40001, but the actual bus read doesn't use the address, but instead the offset
            // we'll support th user passing in the definition either way
            startRegister -= 40001;
        }

        if (registerCount > MaxRegisterReadCount) throw new ArgumentException($"A maximum of {MaxRegisterReadCount} registers can be retrieved at one time");

        var message = GenerateReadMessage(modbusAddress, ModbusFunction.ReadHoldingRegister, startRegister, registerCount);
        await _syncRoot.WaitAsync();

        byte[] result;

        try
        {
            await DeliverMessage(message);
            result = await ReadResult(ModbusFunction.ReadHoldingRegister);
        }
        finally
        {
            _syncRoot.Release();
        }

        var registers = new ushort[registerCount];
        for (var i = 0; i < registerCount; i++)
        {
            registers[i] = (ushort)((result[i * 2] << 8) | (result[i * 2 + 1]));
        }
        return registers;
    }

    /// <summary>
    /// Reads input registers from the Modbus device.
    /// </summary>
    /// <param name="modbusAddress">The Modbus device address.</param>
    /// <param name="startRegister">The starting register number to read from.</param>
    /// <param name="registerCount">The number of registers to read.</param>
    /// <returns>An array of ushort values representing the registers.</returns>
    public async Task<ushort[]> ReadInputRegisters(byte modbusAddress, ushort startRegister, int registerCount)
    {
        if (startRegister > 30000)
        {
            // input registers are defined as starting at 30001, but the actual bus read doesn't use the address, but instead the offset
            // we'll support th user passing in the definition either way
            startRegister -= 30001;
        }

        if (registerCount > MaxRegisterReadCount) throw new ArgumentException($"A maximum of {MaxRegisterReadCount} registers can be retrieved at one time");

        var message = GenerateReadMessage(modbusAddress, ModbusFunction.ReadInputRegister, startRegister, registerCount);
        await _syncRoot.WaitAsync();

        byte[] result;

        try
        {
            await DeliverMessage(message);
            result = await ReadResult(ModbusFunction.ReadHoldingRegister);
        }
        finally
        {
            _syncRoot.Release();
        }

        var registers = new ushort[result.Length / 2];
        for (var i = 0; i < registers.Length; i++)
        {
            registers[i] = (ushort)((result[i * 2] << 8) | (result[i * 2 + 1]));
        }
        return registers;
    }

    /// <inheritdoc/>
    public async Task WriteCoil(byte modbusAddress, ushort register, bool value)
    {
        var data = value ? new byte[] { 0xff, 0x00 } : new byte[] { 0x00, 0x00 };

        var message = GenerateWriteMessage(modbusAddress, ModbusFunction.WriteCoil, register, data);

        await _syncRoot.WaitAsync();
        try
        {
            await DeliverMessage(message);
            await ReadResult(ModbusFunction.WriteCoil);
        }
        finally
        {
            _syncRoot.Release();
        }
    }

    /// <inheritdoc/>
    public async Task WriteMultipleCoils(byte modbusAddress, ushort startRegister, IEnumerable<bool> values)
    {
        // Reduce bool value list to 8 bit byte array
        ushort byteArrayLength = (ushort)((values.Count() / 8) + (ushort)((values.Count() % 8) > 0 ? 1 : 0)); // Calc # 8 bit bytes needed to TX
        byte[] msgSegment = new byte[2 + 1 + byteArrayLength]; // StartAddr + coils + value bytes

        msgSegment[0] = (byte)(values.Count() >> 8);    // Qty of coils HI
        msgSegment[1] = (byte)(values.Count() & 0xFF);  // Qty of coils LO
        msgSegment[2] = (byte)byteArrayLength;          // Byte count

        new BitArray(values.ToArray()).CopyTo(msgSegment, 3); // Concatinate bool binary values list as converted bytes

        var message = GenerateWriteMessage(modbusAddress, ModbusFunction.WriteMultipleCoils, startRegister, msgSegment);
        await _syncRoot.WaitAsync();

        try
        {
            await DeliverMessage(message);
            await ReadResult(ModbusFunction.WriteMultipleRegisters);
        }
        catch (Exception ex)
        {
            Resolver.Log.Error($"WriteMultipleCoils Exception [{ex.Message}]");
        }
        finally
        {
            _syncRoot.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<bool[]> ReadCoils(byte modbusAddress, ushort startCoil, int coilCount)
    {
        if (coilCount > MaxRegisterReadCount) throw new ArgumentException($"A maximum of {MaxRegisterReadCount} coils can be retrieved at one time");

        var message = GenerateReadMessage(modbusAddress, ModbusFunction.ReadCoil, startCoil, coilCount);
        await _syncRoot.WaitAsync();

        byte[] result;

        try
        {
            await DeliverMessage(message);
            result = await ReadResult(ModbusFunction.ReadHoldingRegister);
        }
        finally
        {
            _syncRoot.Release();
        }

        int currentValue = 0;
        int currentBit;
        var values = new bool[coilCount];

        for (var i = 0; i < result.Length; i++)
        {
            currentBit = 0;
            while (currentValue < coilCount && currentBit < 8)
            {
                var r = result[i] & (1 << currentBit++);
                values[currentValue++] = r != 0;
            }
        }

        return values;
    }

    private float ConvertUShortsToFloat(ushort high, ushort low)
    {
        // Combine the high and low values into a single uint
        uint input = (uint)(((high & 0x00FF) << 24) |
                            ((high & 0xFF00) << 8) |
                             (low & 0x00FF) << 8 |
                              low >> 8);

        // Get the sign bit
        uint signBit = (input >> 31) & 1;
        int sign = 1 - (int)(2 * signBit);
        // Get the exponent bits
        var exponentBits = ((input >> 23) & 0xFF);
        var exponent = exponentBits - 127;
        // Get the fraction
        var fractionBits = (input & 0x7FFFFF);
        var fraction = 1.0 + fractionBits / Math.Pow(2, 23);

        // get the value
        return (float)(sign * fraction * Math.Pow(2, exponent));
    }
}
