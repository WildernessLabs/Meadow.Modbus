using System;
using System.Net;
using System.Threading.Tasks;

namespace Meadow.Modbus
{
    public abstract class ModbusClientBase : IModbusBusClient
    {
        public event EventHandler Disconnected = delegate { };
        public event EventHandler Connected = delegate { };

        private bool m_connected;

        protected abstract byte[] GenerateWriteMessage(byte modbusAddress, ModbusFunction function, ushort register, byte[] data);
        protected abstract byte[] GenerateReadMessage(byte modbusAddress, ModbusFunction function, ushort startRegister, int registerCount);

        protected abstract Task DeliverMessage(byte[] message);
        protected abstract Task<byte[]> ReadResult(ModbusFunction function, int expectedBytes);

        public abstract Task Connect();
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

        public async Task WriteHoldingRegister(byte modbusAddress, ushort register, ushort value)
        {
            // swap endianness, because Modbus
            var data = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)value));
            var message = GenerateWriteMessage(modbusAddress, ModbusFunction.WriteRegister, register, data);
            await DeliverMessage(message);
            await ReadResult(ModbusFunction.WriteRegister, 12);
        }

        public async Task<ushort[]> ReadHoldingRegisters(byte modbusAddress, ushort startRegister, int registerCount)
        {
            var message = GenerateReadMessage(modbusAddress, ModbusFunction.ReadHoldingRegister, startRegister, registerCount);
            await DeliverMessage(message);
            var result = await ReadResult(ModbusFunction.ReadHoldingRegister, 9 + 2 * registerCount);

            var registers = new ushort[registerCount];
            for (var i = 0; i < registerCount; i++)
            {
                registers[i] = (ushort)((result[i * 2] << 8) | (result[i * 2 + 1]));
            }
            return registers;
        }
    }
}
