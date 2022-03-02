using System;
using System.Net;

namespace Meadow.Modbus
{
    internal class RawMessage
    {
        private byte[] m_data;

        public RawMessage(byte[] buffer, int start, int length)
        {
            m_data = new byte[length];
            Buffer.BlockCopy(buffer, start, m_data, 0, length);

            switch (Function)
            {
                // write multiple always starts with a count header
                case ModbusFunction.WriteMultipleCoils:
                    WriteCoilValues = new byte[m_data[5]];
                    Buffer.BlockCopy(m_data, 6, WriteCoilValues, 0, WriteCoilValues.Length);
                    break;
                case ModbusFunction.WriteMultipleRegisters:
                    WriteRegisterValues = new byte[m_data[5]];
                    Buffer.BlockCopy(m_data, 6, WriteRegisterValues, 0, WriteRegisterValues.Length);
                    break;
                default: 
                    throw new NotSupportedException();
            }
        }

        public ModbusFunction Function => (ModbusFunction)m_data[0];
        public ushort ReadStart => (ushort)IPAddress.NetworkToHostOrder(BitConverter.ToInt16(m_data, 1));
        public short ReadLength => IPAddress.NetworkToHostOrder(BitConverter.ToInt16(m_data, 3));

        public ushort WriteRegisterAddress => (ushort)IPAddress.NetworkToHostOrder(BitConverter.ToInt16(m_data, 1));
        public ushort WriteRegisterValue => (ushort)IPAddress.NetworkToHostOrder(BitConverter.ToInt16(m_data, 3));

        public ushort WriteCoilAddress => (ushort)IPAddress.NetworkToHostOrder(BitConverter.ToInt16(m_data, 1));
        public bool WriteCoilValue => m_data[3] != 0;

        public short WriteCoilCount => m_data[4];
        public byte[]? WriteCoilValues { get; private set; }

        public short WriteRegisterCount => m_data[4];
        public byte[]? WriteRegisterValues { get; private set; }
    }
}
