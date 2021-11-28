using System;
using System.Net;

namespace Meadow.Modbus
{
    public abstract class ModbusClientBase : IModbusBusClient
    {
        public event EventHandler Disconnected = delegate { };
        public event EventHandler Connected = delegate { };

        private bool m_connected;

        protected abstract byte[] GenerateWriteMessage(byte modbusAddress, ModbusFunction function, ushort register, byte[] data);
        protected abstract byte[] GenerateReadMessage(byte modbusAddress, ModbusFunction function, ushort startRegister, ushort registerCount);

        protected abstract void DeliverMessage(byte[] message);
        protected abstract byte[] ReadResult();

        public abstract void Connect();
        public abstract void Disconnect();

        public bool IsConnected
        {
            get => m_connected;
            protected set
            {
                m_connected = value;

                if (m_connected)
                {
                    Connected?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    Disconnected?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public void WriteHoldingRegister(byte modbusAddress, ushort register, ushort value)
        {
            // swap endianness, because Modbus
            var data = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)value));
            var message = GenerateWriteMessage(modbusAddress, ModbusFunction.WriteRegister, register, data);
            DeliverMessage(message);
            var result = ReadResult();
        }

        public ushort[] ReadHoldingRegisters(byte modbusAddress, ushort startRegister, ushort registerCount)
        {
            var message = GenerateReadMessage(modbusAddress, ModbusFunction.ReadHoldingRegister, startRegister, registerCount);
            DeliverMessage(message);
            var result = ReadResult();

            return null;
        }
    }
}
