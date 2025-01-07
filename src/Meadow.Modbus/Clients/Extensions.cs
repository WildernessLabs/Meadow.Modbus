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
    /// Converts ushort registers to a single Int32, starting at a specific offset
    /// </summary>
    /// <param name="registers">The registers</param>
    /// <param name="startOffset">The offset in the registers to begine extraction</param>
    /// <param name="swappedWords">True to convert from big-endian words</param>
    public static int ExtractInt32(this ushort[] registers, int startOffset = 0, bool swappedWords = false)
    {
        return registers.AsSpan().ExtractInt32(startOffset, swappedWords);
    }

    /// <summary>
    /// Converts ushort registers to a single Int32, starting at a specific offset
    /// </summary>
    /// <param name="registers">The registers</param>
    /// <param name="startOffset">The offset in the registers to begine extraction</param>
    /// <param name="swappedWords">True to convert from big-endian words</param>
    public static int ExtractInt32(this Span<ushort> registers, int startOffset = 0, bool swappedWords = false)
    {
        if (swappedWords)
        {
            byte[] value = BitConverter.GetBytes(registers[startOffset + 0])
                .Concat(BitConverter.GetBytes(registers[startOffset + 1]))
                .ToArray();

            return BitConverter.ToInt32(value, 0);
        }
        else
        {
            byte[] value = BitConverter.GetBytes(registers[startOffset + 1])
                .Concat(BitConverter.GetBytes(registers[startOffset + 0]))
                .ToArray();

            return BitConverter.ToInt32(value, 0);
        }
    }

    /// <summary>
    /// Converts ushort registers to a single UInt32, starting at a specific offset
    /// </summary>
    /// <param name="registers">The registers</param>
    /// <param name="startOffset">The offset in the registers to begine extraction</param>
    /// <param name="swappedWords">True to convert from big-endian words</param>
    public static uint ExtractUInt32(this Span<ushort> registers, int startOffset = 0, bool swappedWords = false)
    {
        if (swappedWords)
        {
            byte[] value = BitConverter.GetBytes(registers[startOffset + 0])
                .Concat(BitConverter.GetBytes(registers[startOffset + 1]))
                .ToArray();

            return BitConverter.ToUInt32(value, 0);
        }
        else
        {
            byte[] value = BitConverter.GetBytes(registers[startOffset + 1])
                .Concat(BitConverter.GetBytes(registers[startOffset + 0]))
                .ToArray();

            return BitConverter.ToUInt32(value, 0);
        }
    }

    /// <summary>
    /// Converts ushort registers to a single Int64, starting at a specific offset
    /// </summary>
    /// <param name="registers">The registers</param>
    /// <param name="startOffset">The offset in the registers to begine extraction</param>
    /// <param name="swappedWords">True to convert from big-endian words</param>
    public static long ExtractInt64(this Span<ushort> registers, int startOffset = 0, bool swappedWords = false)
    {
        if (swappedWords)
        {
            byte[] value = BitConverter.GetBytes(registers[startOffset + 0])
                .Concat(BitConverter.GetBytes(registers[startOffset + 1]))
                .Concat(BitConverter.GetBytes(registers[startOffset + 2]))
                .Concat(BitConverter.GetBytes(registers[startOffset + 3]))
                .ToArray();

            return BitConverter.ToInt64(value, 0);
        }
        else
        {
            byte[] value = BitConverter.GetBytes(registers[startOffset + 3])
                .Concat(BitConverter.GetBytes(registers[startOffset + 2]))
                .Concat(BitConverter.GetBytes(registers[startOffset + 1]))
                .Concat(BitConverter.GetBytes(registers[startOffset + 0]))
                .ToArray();

            return BitConverter.ToInt64(value, 0);
        }
    }

    /// <summary>
    /// Converts ushort registers to a single UInt64, starting at a specific offset
    /// </summary>
    /// <param name="registers">The registers</param>
    /// <param name="startOffset">The offset in the registers to begine extraction</param>
    /// <param name="swappedWords">True to convert from big-endian words</param>
    public static ulong ExtractUInt64(this Span<ushort> registers, int startOffset = 0, bool swappedWords = false)
    {
        if (swappedWords)
        {
            byte[] value = BitConverter.GetBytes(registers[startOffset + 0])
                .Concat(BitConverter.GetBytes(registers[startOffset + 1]))
                .Concat(BitConverter.GetBytes(registers[startOffset + 2]))
                .Concat(BitConverter.GetBytes(registers[startOffset + 3]))
                .ToArray();

            return BitConverter.ToUInt64(value, 0);
        }
        else
        {
            byte[] value = BitConverter.GetBytes(registers[startOffset + 3])
                .Concat(BitConverter.GetBytes(registers[startOffset + 2]))
                .Concat(BitConverter.GetBytes(registers[startOffset + 1]))
                .Concat(BitConverter.GetBytes(registers[startOffset + 0]))
                .ToArray();

            return BitConverter.ToUInt64(value, 0);
        }
    }

    /// <summary>
    /// Converts 4 ushort registers to a IEEE 64 floating point, starting at a specific offset
    /// </summary>
    /// <param name="registers">The registers</param>
    /// <param name="startOffset">The offset in the registers to begine extraction</param>
    /// <param name="swappedWords">True to convert from big-endian words</param>
    public static double ExtractDouble(this ushort[] registers, int startOffset = 0, bool swappedWords = false)
    {
        return registers.AsSpan().ExtractDouble(startOffset, swappedWords);
    }

    /// <summary>
    /// Converts 4 ushort registers to a IEEE 64 floating point, starting at a specific offset
    /// </summary>
    /// <param name="registers">The registers</param>
    /// <param name="startOffset">The offset in the registers to begine extraction</param>
    /// <param name="swappedWords">True to convert from big-endian words</param>
    public static double ExtractDouble(this Span<ushort> registers, int startOffset = 0, bool swappedWords = false)
    {
        if (registers.Length < 4) throw new ArgumentException("registers does not contain enough data to extract a double");

        if (swappedWords)
        {
            byte[] value = BitConverter.GetBytes(registers[startOffset + 0])
                .Concat(BitConverter.GetBytes(registers[startOffset + 1]))
                .Concat(BitConverter.GetBytes(registers[startOffset + 2]))
                .Concat(BitConverter.GetBytes(registers[startOffset + 3]))
                .ToArray();

            return BitConverter.ToDouble(value, 0);
        }
        else
        {
            byte[] value = BitConverter.GetBytes(registers[startOffset + 3])
                .Concat(BitConverter.GetBytes(registers[startOffset + 2]))
                .Concat(BitConverter.GetBytes(registers[startOffset + 1]))
                .Concat(BitConverter.GetBytes(registers[startOffset + 0]))
                .ToArray();

            return BitConverter.ToDouble(value, 0);
        }
    }

    /// <summary>
    /// Converts 2 ushort registers to a IEEE 32 floating point, starting at a specific offset
    /// </summary>
    /// <param name="registers">The registers</param>
    /// <param name="startOffset">The offset in the registers to begine extraction</param>
    /// <param name="swappedWords">True to convert from big-endian words</param>
    public static float ExtractSingle(this ushort[] registers, int startOffset = 0, bool swappedWords = false)
    {
        return registers.AsSpan().ExtractSingle(startOffset, swappedWords);
    }

    /// <summary>
    /// Converts 2 ushort registers to a IEEE 32 floating point, starting at a specific offset
    /// </summary>
    /// <param name="registers">The registers</param>
    /// <param name="startOffset">The offset in the registers to begine extraction</param>
    /// <param name="swappedWords">True to convert from big-endian words</param>
    public static float ExtractSingle(this Span<ushort> registers, int startOffset = 0, bool swappedWords = false)
    {
        if (swappedWords)
        {
            byte[] value = BitConverter.GetBytes(registers[startOffset + 0])
                .Concat(BitConverter.GetBytes(registers[startOffset + 1]))
                .ToArray();

            return BitConverter.ToSingle(value, 0);
        }
        else
        {
            byte[] value = BitConverter.GetBytes(registers[startOffset + 1])
                .Concat(BitConverter.GetBytes(registers[startOffset + 0]))
                .ToArray();

            return BitConverter.ToSingle(value, 0);
        }
    }

    /// <summary>
    /// Converts three UInt16 values into an unsigned 48-bit Mod10 (modulo 10000) format (returned as an Int64).
    /// </summary>
    /// <param name="registers">The registers</param>
    public static long ExtractMod10_48(this Span<ushort> registers)
    {
        // Each registers range is -9,999 to +9,999:
        long R1 = BitConverter.ToInt16(BitConverter.GetBytes(registers[0]).ToArray(), 0);
        long R2 = BitConverter.ToInt16(BitConverter.GetBytes(registers[1]).ToArray(), 0);
        long R3 = BitConverter.ToInt16(BitConverter.GetBytes(registers[2]).ToArray(), 0);

        // R3*10,000^2 + R2*10,000 + R1
        return (R3 * (long)Math.Pow(10000, 2)) + (R2 * 10000) + R1;
    }

    /// <summary>
    /// Converts four UInt16 values into an unsigned 48-bit Mod10 (modulo 10000) format (returned as an Int64).
    /// </summary>
    /// <param name="registers">The registers</param>
    public static long ExtractMod10_64(this Span<ushort> registers)
    {
        // Each registers range is -9,999 to +9,999:
        long R1 = BitConverter.ToInt16(BitConverter.GetBytes(registers[0]).ToArray(), 0);
        long R2 = BitConverter.ToInt16(BitConverter.GetBytes(registers[1]).ToArray(), 0);
        long R3 = BitConverter.ToInt16(BitConverter.GetBytes(registers[2]).ToArray(), 0);
        long R4 = BitConverter.ToInt16(BitConverter.GetBytes(registers[3]).ToArray(), 0);

        // R3*10,000^2 + R2*10,000 + R1
        return (R4 * (long)Math.Pow(10000, 3)) + (R3 * (long)Math.Pow(10000, 2)) + (R2 * 10000) + R1;
    }
}
