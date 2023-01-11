using System;
using System.Collections.Generic;
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
        /// Writes multiple values to holding registers (modbus function 16)
        /// </summary>
        /// <param name="modbusAddress">The target device modbus address</param>
        /// <param name="startRegister">The first register to begin writing</param>
        /// <param name="values">The registers (16-bit values) to write</param>
        /// <returns></returns>
        Task WriteHoldingRegisters(byte modbusAddress, ushort startRegister, IEnumerable<ushort> values);

        /// <summary>
        /// Reads the requested number of holding registers from a device
        /// </summary>
        /// <param name="modbusAddress"></param>
        /// <param name="startRegister"></param>
        /// <param name="registerCount"></param>
        /// <returns></returns>
        Task<ushort[]> ReadHoldingRegisters(byte modbusAddress, ushort startRegister, int registerCount);

        /// <summary>
        /// Reads the requested number of floats from the holding registers
        /// Each float is two sequential registers
        /// </summary>
        /// <param name="modbusAddress"></param>
        /// <param name="startRegister"></param>
        /// <param name="floatCount"></param>
        /// <returns></returns>
        Task<float[]> ReadHoldingRegistersFloat(byte modbusAddress, ushort startRegister, int floatCount);

        Task WriteCoil(byte modbusAddress, ushort register, bool value);
        Task<bool[]> ReadCoils(byte modbusAddress, ushort startCoil, int coilCount);
    }
}
