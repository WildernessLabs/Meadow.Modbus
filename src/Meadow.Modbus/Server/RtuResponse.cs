using System;

namespace Meadow.Modbus;

internal class RtuResponse
{
    private byte[] m_data;

    private RtuResponse(byte[] data)
    {
        m_data = data;
    }

    public static RtuResponse CreateErrorResponse(ModbusErrorResult mer)
    {
        throw new NotImplementedException();
    }

    public static RtuResponse CreateReadResponse(ModbusFunction function, byte modbusAddress, ModbusReadResult mrr)
    {
        if (mrr.Data.Length > 254) throw new ArgumentException("Register data is too long.  Expected < 255 bytes");

        // assume header is 3 bytes (ignore read multiple for now)

        var data = new byte[5 + mrr.Data.Length];
        data[0] = modbusAddress;
        data[1] = (byte)function;
        data[2] = (byte)mrr.Data.Length;
        Array.Copy(mrr.Data, 0, data, 3, mrr.Data.Length);

        RtuHelpers.FillCRC(data);

        return new RtuResponse(data);
    }

    public byte[] Serialize()
    {
        return m_data;
    }
}
