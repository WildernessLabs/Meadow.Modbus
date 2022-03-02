using System;
using System.Threading.Tasks;

namespace Meadow.Modbus
{
    public interface IModbusBusClient
    {
        event EventHandler Disconnected;
        event EventHandler Connected;

        public bool IsConnected { get; }

        Task Connect();
        void Disconnect();

        /// <summary>
        /// Writes a single value to the given register on a device
        /// </summary>
        /// <param name="modbusAddress"></param>
        /// <param name="register"></param>
        /// <param name="value"></param>
        Task WriteHoldingRegister(byte modbusAddress, ushort register, ushort value);

        /// <summary>
        /// Reads the requested number of holding registers from a device
        /// </summary>
        /// <param name="modbusAddress"></param>
        /// <param name="startRegister"></param>
        /// <param name="registerCount"></param>
        /// <returns></returns>
        Task<ushort[]> ReadHoldingRegisters(byte modbusAddress, ushort startRegister, int registerCount);

        Task WriteCoil(byte modbusAddress, ushort register, bool value);
        Task<bool[]> ReadCoils(byte modbusAddress, ushort startCoil, int coilCount);
    }
}
