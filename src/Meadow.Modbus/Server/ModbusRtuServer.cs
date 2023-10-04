using Meadow.Hardware;
using System;
using System.Threading;

namespace Meadow.Modbus;

public class ModbusRtuServer : IModbusServer
{
    private ISerialPort _port;
    private bool _signalStop;

    /// <inheritdoc/>
    public event ReadDelegate? ReadCoilRequest = default!;
    /// <inheritdoc/>
    public event ReadDelegate? ReadDiscreteRequest = default!;
    /// <inheritdoc/>
    public event ReadDelegate? ReadHoldingRegisterRequest = default!;
    /// <inheritdoc/>
    public event ReadDelegate? ReadInputRegisterRequest = default!;
    /// <inheritdoc/>
    public event WriteCoilDelegate? WriteCoilRequest = default!;
    /// <inheritdoc/>
    public event WriteRegisterDelegate? WriteRegisterRequest;
    /// <summary>
    /// Event that is raised when a CRC (Cyclic Redundancy Check) error is detected.
    /// </summary>
    public event EventHandler? CrcErrorDetected;

    /// <inheritdoc/>
    public bool IsDisposed { get; private set; }

    /// <inheritdoc/>
    public bool IsRunning
    {
        get { return _port.IsOpen; }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ModbusRtuServer"/> class using the specified serial port.
    /// </summary>
    /// <param name="serverPort">The serial port to use for communication.</param>
    public ModbusRtuServer(ISerialPort serverPort)
    {
        _port = serverPort;
    }

    /// <inheritdoc/>
    public void Start()
    {
        if (!IsRunning)
        {
            new Thread(ServerThreadProc)
                .Start();
        }
    }

    /// <inheritdoc/>
    public void Stop()
    {
        if (IsRunning)
        {
            _signalStop = true;
        }
    }

    private void ServerThreadProc()
    {
        _port.Open();

        var buffer = new byte[1024];
        int read = 0;

        ModbusFunction function;
        IModbusResult? result = null;
        RtuResponse? response;
        byte modbusAddress;

        while (!_signalStop)
        {
            read = 0;
            response = null;

            // all packets are > 4 bytes, this is enough to get the function and target address
            while (_port.BytesToRead == 0)
            {
                Thread.Sleep(100);
            }

            while (read < 4)
            {
                read += _port.Read(buffer, read, 4 - read);
            }

            modbusAddress = buffer[0];
            function = (ModbusFunction)buffer[1];
            ushort expectedCrc;
            ushort actualCrc;
            ushort? startRegister = default;
            ushort? writeValue = default;

            switch (function)
            {
                case ModbusFunction.ReadHoldingRegister:
                    // always 8 bytes, read 4 more
                    while (read < 8)
                    {
                        read += _port.Read(buffer, read, 8 - read);
                    }

                    expectedCrc = RtuHelpers.Crc(buffer, 0, 6);
                    actualCrc = (ushort)(buffer[6] | buffer[7] << 8);

                    if (expectedCrc != actualCrc)
                    {
                        // the spec says if there's a CRC error, the server will do nothing (not respond)
                        CrcErrorDetected?.Invoke(this, EventArgs.Empty);
                        result = null;
                    }
                    else
                    {
                        result = ReadHoldingRegisterRequest?.Invoke(
                            buffer[0],
                            (ushort)((buffer[2] << 8) | buffer[3]),
                            (short)((buffer[4] << 8) | buffer[5]));
                    }

                    break;
                case ModbusFunction.ReadInputRegister:
                    // always 8 bytes, read 4 more
                    while (read < 8)
                    {
                        read += _port.Read(buffer, read, 8 - read);
                    }

                    expectedCrc = RtuHelpers.Crc(buffer, 0, 6);
                    actualCrc = (ushort)(buffer[6] | buffer[7] << 8);

                    if (expectedCrc != actualCrc)
                    {
                        // the spec says if there's a CRC error, the server will do nothing (not respond)
                        CrcErrorDetected?.Invoke(this, EventArgs.Empty);
                        result = null;
                    }
                    else
                    {
                        result = ReadInputRegisterRequest?.Invoke(
                        buffer[0],
                        (ushort)((buffer[2] << 8) | buffer[3]),
                        (short)((buffer[4] << 8) | buffer[5]));
                    }

                    break;
                case ModbusFunction.WriteRegister:
                    while (read < 8)
                    {
                        read += _port.Read(buffer, read, 8 - read);
                    }

                    expectedCrc = RtuHelpers.Crc(buffer, 0, 6);
                    actualCrc = (ushort)(buffer[6] | buffer[7] << 8);

                    if (expectedCrc != actualCrc)
                    {
                        // the spec says if there's a CRC error, the server will do nothing (not respond)
                        CrcErrorDetected?.Invoke(this, EventArgs.Empty);
                        result = null;
                    }
                    else
                    {
                        startRegister = (ushort)((buffer[2] << 8) | buffer[3]);
                        writeValue = (ushort)((buffer[4] << 8) | buffer[5]);

                        result = WriteRegisterRequest?.Invoke(
                        buffer[0],
                        startRegister.Value,
                        new ushort[]
                        {
                            writeValue.Value,
                        });
                    }
                    break;
                case ModbusFunction.WriteMultipleRegisters:
                    // get the length
                    while (read < 7)
                    {
                        read += _port.Read(buffer, read, 7 - read);
                    }

                    var registerCount = (ushort)((buffer[4] << 8) | buffer[5]);
                    var totalLength = 7 + (registerCount * 2) + 2;
                    while (read < totalLength)
                    {
                        read += _port.Read(buffer, read, totalLength - 7);
                    }

                    expectedCrc = RtuHelpers.Crc(buffer, 0, totalLength - 2);
                    actualCrc = (ushort)(buffer[totalLength - 2] | buffer[totalLength - 1] << 8);

                    if (expectedCrc != actualCrc)
                    {
                        // the spec says if there's a CRC error, the server will do nothing (not respond)
                        CrcErrorDetected?.Invoke(this, EventArgs.Empty);
                        result = null;
                    }
                    else
                    {
                        startRegister = (ushort)((buffer[2] << 8) | buffer[3]);
                        writeValue = registerCount;

                        var registers = new ushort[registerCount];
                        for (var r = 0; r < registerCount; r++)
                        {
                            registers[r] = (ushort)((buffer[r * 2 + 7] << 8) | buffer[r * 2 + 8]);
                        }

                        result = WriteRegisterRequest?.Invoke(
                            buffer[0],
                            startRegister.Value,
                            registers);
                    }
                    break;
                default:
                    result = new ModbusErrorResult(ModbusErrorCode.IllegalFunction);
                    break;
            }

            if (result is ModbusReadResult mrr)
            {
                response = RtuResponse.CreateReadResponse(function, modbusAddress, mrr);
            }
            if (result is ModbusWriteResult mwr)
            {
                response = RtuResponse.CreateWriteResponse(function, modbusAddress, startRegister!.Value, writeValue!.Value, mwr);
            }
            else if (result is ModbusErrorResult mer)
            {
                response = RtuResponse.CreateErrorResponse(function, modbusAddress, mer);
            }

            if (response != null)
            {
                var data = response.Serialize();
                _port.Write(data);
            }
        }

        _port.Close();
    }

    /// <inheritdoc/>
    protected virtual void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            if (disposing)
            {
                _port.Dispose();
            }

            IsDisposed = true;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
