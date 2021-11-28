using System;

namespace Meadow.Modbus
{
    public interface IModbusBusClient
    {
        event EventHandler Disconnected;
        event EventHandler Connected;

        public bool IsConnected { get; }

        void Connect();
        void Disconnect();

        void WriteSingleRegister(byte address, ushort register, short value);
    }
}
