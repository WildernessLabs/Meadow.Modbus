using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Meadow.Modbus;

/// <summary>
/// Interface for a Modbus bus client.
/// </summary>
public interface IModbusBusClient
{
    /// <summary>
    /// Event that is raised when the client is disconnected.
    /// </summary>
    event EventHandler Disconnected;

    /// <summary>
    /// Event that is raised when the client is connected.
    /// </summary>
    event EventHandler Connected;

    /// <summary>
    /// Gets a value indicating whether the client is connected.
    /// </summary>
    public bool IsConnected { get; }

    /// <summary>
    /// Asynchronously connects the client.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task Connect();

    /// <summary>
    /// Disconnects the client.
    /// </summary>
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

    /// <summary>
    /// Writes a coil value to the given register on a device.
    /// </summary>
    /// <param name="modbusAddress">The Modbus address of the device.</param>
    /// <param name="register">The register to write to.</param>
    /// <param name="value">The value to write.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task WriteCoil(byte modbusAddress, ushort register, bool value);

    /// <summary>
    /// Writes multiple coil values to the given registers on a device.
    /// </summary>
    /// <param name="modbusAddress">The Modbus address of the device.</param>
    /// <param name="startRegister">The first register to begin writing.</param>
    /// <param name="values">The coil values to write.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task WriteMultipleCoils(byte modbusAddress, ushort startRegister, IEnumerable<bool> values);

    /// <summary>
    /// Reads the requested number of coils from a device.
    /// </summary>
    /// <param name="modbusAddress">The Modbus address of the device.</param>
    /// <param name="startCoil">The first coil to begin reading.</param>
    /// <param name="coilCount">The number of coils to read.</param>
    /// <returns>An array of coil values read from the device.</returns>
    Task<bool[]> ReadCoils(byte modbusAddress, ushort startCoil, int coilCount);
}
