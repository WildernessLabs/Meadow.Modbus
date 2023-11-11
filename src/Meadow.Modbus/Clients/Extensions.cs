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
}
