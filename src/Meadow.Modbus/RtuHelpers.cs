namespace Meadow.Modbus;

internal static class RtuHelpers
{
    public static ushort Crc(byte[] data, int index, int count)
    {
        ushort crc = 0xFFFF;
        char lsb;

        for (int i = index; i < count; i++)
        {
            crc = (ushort)(crc ^ data[i]);

            for (int j = 0; j < 8; j++)
            {
                lsb = (char)(crc & 0x0001);
                crc = (ushort)((crc >> 1) & 0x7fff);

                if (lsb == 1)
                    crc = (ushort)(crc ^ 0xa001);
            }
        }

        return crc;
    }

    public static void FillCRC(byte[] message)
    {
        var crc = Crc(message, 0, message.Length - 2);

        // fill in the CRC (last 2 bytes) - big-endian
        message[message.Length - 1] = (byte)((crc >> 8) & 0xff);
        message[message.Length - 2] = (byte)(crc & 0xff);
    }
}
