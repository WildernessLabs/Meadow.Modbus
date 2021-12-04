using System;
using System.Net;

namespace Meadow.Modbus
{
    internal class Response
    {
        private byte[] m_data;

        private Response(byte[] data)
        {
            m_data = data;
        }

        public static Response CreateWriteResponse(ModbusFunction function, short requestID, byte unit, short lengthWritten)
        {
            var data = new byte[12];

            // we have to swap byte order going back out
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.NetworkToHostOrder(requestID)), 0, data, 0, 2);
            data[5] = 6; // length is always 6
            data[6] = unit;
            data[7] = (byte)function;

            // 2 bytes of coil address
            // values written (ff == on, 00 == off)

            // if we're responsing to a write request, the result payload is at offset 10 and is always 2 bytes (written length?)

            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.NetworkToHostOrder(lengthWritten)), 0, data, 10, 2);

            return new Response(data);
        }

        public static Response CreateWriteRegisterResponse(ModbusFunction function, short requestID, byte unit, ushort registerAddress, ushort valueWritten)
        {
            var data = new byte[12];

            // we have to swap byte order going back out
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.NetworkToHostOrder(requestID)), 0, data, 0, 2);
            data[2] = 0; // protocol == modbus (2 bytes)
            data[3] = 0;
            data[4] = 0; // length is always 6 (2 bytes)
            data[5] = 6;
            data[6] = unit;
            data[7] = (byte)function;

            // 2 bytes of register address - C# will turn a ushort into 4 bytes when calling NetworkToHostOrder, because it's stupid
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.NetworkToHostOrder((short)registerAddress)), 0, data, 8, 2);

            // 2 bytes of what was written
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.NetworkToHostOrder((short)valueWritten)), 0, data, 10, 2);

            return new Response(data);
        }

        public static Response CreateWriteCoilResponse(ModbusFunction function, short requestID, byte unit, ushort coilAddress, bool valueWritten)
        {
            var data = new byte[12];

            // we have to swap byte order going back out
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.NetworkToHostOrder(requestID)), 0, data, 0, 2);
            data[2] = 0; // protocol == modbus (2 bytes)
            data[3] = 0;
            data[4] = 0; // length is always 6 (2 bytes)
            data[5] = 6;
            data[6] = unit;
            data[7] = (byte)function;

            // 2 bytes of coil address - C# will turn a ushort into 4 bytes when calling NetworkToHostOrder, because it's stupid
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.NetworkToHostOrder((short)coilAddress)), 0, data, 8, 2);

            // value written (ff == on, 00 == off)
            data[10] = valueWritten ? (byte)0xff : (byte)0x00;

            // last byte is zero for some reason that's not clear in the spec.  Probably padding?
            data[11] = 0;

            return new Response(data);
        }

        public static Response CreateReadResponse(ModbusFunction function, short requestID, byte unit, byte[] payload)
        {
            var data = new byte[payload.Length + 9];

            // we have to swap byte order going back out
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.NetworkToHostOrder(requestID)), 0, data, 0, 2);
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.NetworkToHostOrder((short)(payload.Length + 3))), 0, data, 4, 2);
            data[6] = unit;
            data[7] = (byte)function;
            data[8] = (byte)payload.Length;

            // if we're responding to a read request, the result payload is at offset 9
            Buffer.BlockCopy(payload, 0, data, 9, payload.Length);

            return new Response(data);
        }

        public static Response CreateErrorResponse(ModbusFunction function, short requestID, byte unit, ModbusErrorCode reason)
        {
            var data = new byte[9];

            // we have to swap byte order going back out
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.NetworkToHostOrder(requestID)), 0, data, 0, 2);
            data[6] = unit;
            data[7] = (byte)((byte)function + 0x80);
            data[8] = (byte)reason;

            return new Response(data);
        }

        public byte[] Serialize()
        {
            return m_data;
        }
    }
}
