using System;
using System.Net;

namespace Meadow.Modbus
{
    public sealed class ModbusReadResult : IModbusResult
    {
        public byte[] Data { get; }

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

        public ModbusReadResult(byte[] data)
        {
            Data = data;
        }
    }
}
