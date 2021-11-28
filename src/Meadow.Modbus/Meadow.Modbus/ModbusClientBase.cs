using System;
using System.Net;

namespace Meadow.Modbus
{
    public abstract class ModbusClientBase : IModbusBusClient
    {
        public event EventHandler Disconnected = delegate { };
        public event EventHandler Connected = delegate { };

        private bool m_connected;

        protected abstract byte[] GenerateMessage(byte modbusAddress, ModbusFunction function, ushort register, byte[] data);
        protected abstract void DeliverMessage(byte[] message);

        public abstract void Connect();
        public abstract void Disconnect();

        public bool IsConnected
        {
            get => m_connected;
            protected set
            {
                m_connected = value;

                if(m_connected)
                {
                    Connected?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    Disconnected?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public void WriteSingleRegister(byte modbusAddress, ushort register, short value)
        {
            // swap endianness, because Modbus
            var data = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(value));
            var message = GenerateMessage(modbusAddress, ModbusFunction.WriteRegister, register, data);
            DeliverMessage(message);
        }
    }
}
