using System;
using System.Net;

namespace Meadow.Modbus;

/// <summary>
/// Represents a Modbus read result.
/// </summary>
public sealed class ModbusReadResult : IModbusResult
{
    /// <summary>
    /// Gets the data associated with the read result.
    /// </summary>
    public byte[] Data { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ModbusReadResult"/> class with coil data.
    /// </summary>
    /// <param name="coilData">The coil data.</param>
    public ModbusReadResult(bool[] coilData)
    {
        Data = new byte[coilData.Length / 8 + ((coilData.Length % 8 != 0) ? 1 : 0)];

        // coil data is stored LSB first in modbus
        var destinationIndex = 0;
        var bit = 0;
        byte b = 0;
        for (int i = 0; i < coilData.Length; i++)
        {
            if (i % 8 == 0)
            {
                b = 0;
                bit = 0;
            }
            if (coilData[i])
            {
                b |= (byte)(1 << bit);
            }
            if (++bit == 8)
            {
                Data[destinationIndex++] = b;
            }
        }
        if (destinationIndex < Data.Length)
        {
            Data[destinationIndex] = b;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ModbusReadResult"/> class with ushort register data.
    /// </summary>
    /// <param name="registerData">The ushort register data.</param>
    public ModbusReadResult(ushort[] registerData)
    {
        Data = new byte[registerData.Length * 2];

        // registers need byte swapping
        var index = 0;
        foreach (var r in registerData)
        {
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)r)), 0, Data, index, 2);
            index += 2;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ModbusReadResult"/> class with short register data.
    /// </summary>
    /// <param name="registerData">The short register data.</param>
    public ModbusReadResult(short[] registerData)
    {
        Data = new byte[registerData.Length * 2];

        // registers need byte swapping
        var index = 0;
        foreach (var r in registerData)
        {
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(r)), 0, Data, index, 2);
            index += 2;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ModbusReadResult"/> class with the provided data.
    /// </summary>
    /// <param name="data">The data associated with the read result.</param>
    public ModbusReadResult(byte[] data)
    {
        Data = data;
    }
}
