using System;
using System.Linq;

namespace Meadow.Modbus;

/// <summary>
/// Extension methods for Modbus functions
/// </summary>
public static class Extensions
{
    /// <summary>
    /// Converts a set of Modbus registers (ushort[]) to integers (int[]) assuming little-endina ordering
    /// </summary>
    /// <param name="registers"></param>
    /// <returns></returns>
    public static int[] ConvertRegistersToInt32(this ushort[] registers)
    {
        var values = new int[registers.Length / 2];

        var index = 0;
        var i = 0;
        // Need to byte swap
        for (; i < registers.Length; i += 2, index++)
        {
            values[index] = ((registers[i] << 8) | (registers[i] >> 8) | (registers[i + 1] << 8) | (registers[i + 1] >> 8));
        }

        return values;
    }


    /// <summary>
    /// Converts one UInt16 values into a Int16.
    /// </summary>
    /// <param name="words">2 Modbus Words (ushort)</param>
    /// <returns></returns>
    public static Int16 ConvertRegistersToInt16(Span<ushort> words)
    {
        byte[] value = BitConverter.GetBytes(words[0])
            .ToArray();

        return BitConverter.ToInt16(value, 0);
    }

    /// <summary>
    /// Converts one UInt16 values into a UInt16.
    /// </summary>
    /// <param name="words">2 Modbus Words (ushort)</param>
    /// <returns></returns>
    public static UInt16 ConvertRegistersToUInt16(Span<ushort> words)
    {
        return words[0];
    }

    /// <summary>
    /// Converts two UInt16 values into a Int32.
    /// </summary>
    /// <param name="words">2 Modbus Words (ushort)</param>
    /// <param name="swappedWords">Slave operates on big-endian 32-bit integers</param>
    /// <returns></returns>
    public static Int32 ConvertRegistersToInt32(Span<ushort> words, bool swappedWords)
    {
        if (swappedWords)
        {
            byte[] value = BitConverter.GetBytes(words[0])
                .Concat(BitConverter.GetBytes(words[1]))
                .ToArray();

            return BitConverter.ToInt32(value, 0);
        }
        else
        {
            byte[] value = BitConverter.GetBytes(words[1])
                .Concat(BitConverter.GetBytes(words[0]))
                .ToArray();

            return BitConverter.ToInt32(value, 0);
        }
    }

    /// <summary>
    /// Converts two UInt16 values into a UInt32.
    /// </summary>
    /// <param name="words">2 Modbus Words (ushort)</param>
    /// <param name="swappedWords">Slave operates on big-endian 32-bit integers</param>
    /// <returns></returns>
    public static UInt32 ConvertRegistersToUInt32(Span<ushort> words, bool swappedWords)
    {
        if (swappedWords)
        {
            byte[] value = BitConverter.GetBytes(words[0])
                .Concat(BitConverter.GetBytes(words[1]))
                .ToArray();

            return BitConverter.ToUInt32(value, 0);
        }
        else
        {
            byte[] value = BitConverter.GetBytes(words[1])
                .Concat(BitConverter.GetBytes(words[0]))
                .ToArray();

            return BitConverter.ToUInt32(value, 0);
        }
    }

    /// <summary>
    /// Converts four UInt16 values into a Int64.
    /// </summary>
    /// <param name="words">2 Modbus Words (ushort)</param>
    /// <param name="swappedWords">Slave operates on big-endian 32-bit integers</param>
    /// <returns></returns>
    public static Int64 ConvertRegistersToInt64(Span<ushort> words, bool swappedWords)
    {
        if (swappedWords)
        {
            byte[] value = BitConverter.GetBytes(words[0])
                .Concat(BitConverter.GetBytes(words[1]))
                .Concat(BitConverter.GetBytes(words[2]))
                .Concat(BitConverter.GetBytes(words[3]))
                .ToArray();

            return BitConverter.ToInt64(value, 0);
        }
        else
        {
            byte[] value = BitConverter.GetBytes(words[3])
                .Concat(BitConverter.GetBytes(words[2]))
                .Concat(BitConverter.GetBytes(words[1]))
                .Concat(BitConverter.GetBytes(words[0]))
                .ToArray();

            return BitConverter.ToInt64(value, 0);
        }
    }

    /// <summary>
    /// Converts four UInt16 values into a UInt64.
    /// </summary>
    /// <param name="words">2 Modbus Words (ushort)</param>
    /// <param name="swappedWords">Slave operates on big-endian 32-bit integers</param>
    /// <returns></returns>
    public static UInt64 ConvertRegistersToUInt64(Span<ushort> words, bool swappedWords)
    {
        if (swappedWords)
        {
            byte[] value = BitConverter.GetBytes(words[0])
                .Concat(BitConverter.GetBytes(words[1]))
                .Concat(BitConverter.GetBytes(words[2]))
                .Concat(BitConverter.GetBytes(words[3]))
                .ToArray();

            return BitConverter.ToUInt64(value, 0);
        }
        else
        {
            byte[] value = BitConverter.GetBytes(words[3])
                .Concat(BitConverter.GetBytes(words[2]))
                .Concat(BitConverter.GetBytes(words[1]))
                .Concat(BitConverter.GetBytes(words[0]))
                .ToArray();

            return BitConverter.ToUInt64(value, 0);
        }
    }

    /// <summary>
    ///     Converts four UInt16 values into a IEEE 64 floating point format.
    /// </summary>
    /// <param name="words">2 Modbus Words (ushort)</param>
    /// <param name="swappedWords"></param>
    /// <returns>IEEE 64 floating point value.</returns>
    public static double ConvertRegistersToDouble(Span<ushort> words, bool swappedWords)
    {
        if (swappedWords)
        {
            byte[] value = BitConverter.GetBytes(words[0])
                .Concat(BitConverter.GetBytes(words[1]))
                .Concat(BitConverter.GetBytes(words[2]))
                .Concat(BitConverter.GetBytes(words[3]))
                .ToArray();

            return BitConverter.ToDouble(value, 0);
        }
        else
        {
            byte[] value = BitConverter.GetBytes(words[3])
                .Concat(BitConverter.GetBytes(words[2]))
                .Concat(BitConverter.GetBytes(words[1]))
                .Concat(BitConverter.GetBytes(words[0]))
                .ToArray();

            return BitConverter.ToDouble(value, 0);
        }
    }

    /// <summary>
    ///     Converts two UInt16 values into a IEEE 32 floating point format.
    /// </summary>
    /// <param name="words"></param>
    /// <param name="swappedWords"></param>
    /// <returns>IEEE 32 floating point value.</returns>
    public static float ConvertRegistersToSingle(Span<ushort> words, bool swappedWords)
    {
        if (swappedWords)
        {
            byte[] value = BitConverter.GetBytes(words[0])
                .Concat(BitConverter.GetBytes(words[1]))
                .ToArray();

            return BitConverter.ToSingle(value, 0);
        }
        else
        {
            byte[] value = BitConverter.GetBytes(words[1])
                .Concat(BitConverter.GetBytes(words[0]))
                .ToArray();

            return BitConverter.ToSingle(value, 0);
        }
    }


    /// <summary>
    ///     Converts three UInt16 values into an unsigned 48-bit Mod10 (modulo 10000) format (returned as a UINT64).
    /// </summary>
    /// <param name="words"></param>
    /// <returns>UInt64</returns>
    public static UInt64 ConvertRegistersToUMod10_48(Span<ushort> words)
    {
        // Each registers range is 0 to +9,999:
        UInt64 R1 = words[0];
        UInt64 R2 = words[1];
        UInt64 R3 = words[2];

        // R3*10,000^2 + R2*10,000 + R1
        return (R3 * (UInt64)Math.Pow(10000, 2)) + (R2 * 10000) + R1;
    }

    /// <summary>
    ///     Converts three UInt16 values into a signed 48-bit Mod10 (modulo 10000) format (returned as a UINT64).
    /// </summary>
    /// <param name="words"></param>
    /// <returns>Int64</returns>
    public static Int64 ConvertRegistersToMod10_48(Span<ushort> words)
    {
        // Each registers range is -9,999 to +9,999:
        Int64 R1 = BitConverter.ToInt16(BitConverter.GetBytes(words[0]).ToArray(), 0);
        Int64 R2 = BitConverter.ToInt16(BitConverter.GetBytes(words[1]).ToArray(), 0);
        Int64 R3 = BitConverter.ToInt16(BitConverter.GetBytes(words[2]).ToArray(), 0);

        // R3*10,000^2 + R2*10,000 + R1
        return (R3 * (Int64)Math.Pow(10000, 2)) + (R2 * 10000) + R1;
    }

    /// <summary>
    ///     Converts four UInt16 values into an unsigned 64-bit Mod10 (modulo 10000) format (returned as a UINT64).
    /// </summary>
    /// <param name="words"></param>
    /// <returns>UInt64</returns>
    public static UInt64 ConvertRegistersToUMod10_64(Span<ushort> words)
    {
        // Each registers range is 0 to +9,999:
        UInt64 R1 = words[0];
        UInt64 R2 = words[1];
        UInt64 R3 = words[2];
        UInt64 R4 = words[3];

        // R3*10,000^2 + R2*10,000 + R1
        return (R4 * (UInt64)Math.Pow(10000, 3)) + (R3 * (UInt64)Math.Pow(10000, 2)) + (R2 * 10000) + R1;
    }

    /// <summary>
    ///     Converts four UInt16 values into a signed 64-bit Mod10 (modulo 10000) format (returned as a INT64).
    /// </summary>
    /// <param name="words"></param>
    /// <returns>Int64</returns>
    public static Int64 ConvertRegistersToMod10_64(Span<ushort> words)
    {
        // Each registers range is -9,999 to +9,999:
        Int64 R1 = BitConverter.ToInt16(BitConverter.GetBytes(words[0]).ToArray(), 0);
        Int64 R2 = BitConverter.ToInt16(BitConverter.GetBytes(words[1]).ToArray(), 0);
        Int64 R3 = BitConverter.ToInt16(BitConverter.GetBytes(words[2]).ToArray(), 0);
        Int64 R4 = BitConverter.ToInt16(BitConverter.GetBytes(words[3]).ToArray(), 0);

        // R3*10,000^2 + R2*10,000 + R1
        return (R4 * (Int64)Math.Pow(10000, 3)) + (R3 * (Int64)Math.Pow(10000, 2)) + (R2 * 10000) + R1;
    }

}
