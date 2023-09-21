using Meadow.Hardware;
using System;
using System.Threading;

namespace Meadow.Modbus;

public class ModbusRtuServer : IModbusServer
{
    private ISerialPort _port;
    private bool _signalStop;

    public bool IsDisposed { get; private set; }

    public event ReadDelegate? ReadCoilRequest;
    public event ReadDelegate? ReadDiscreteRequest;
    public event ReadDelegate? ReadHoldingRegisterRequest;
    public event ReadDelegate? ReadInputRegisterRequest;
    public event WriteCoilDelegate? WriteCoilRequest;
    public event WriteRegisterDelegate? WriteRegisterRequest;
    public event EventHandler? CrcErrorDetected;

    public bool IsRunning
    {
        get { return _port.IsOpen; }
    }

    public ModbusRtuServer(ISerialPort serverPort)
    {
        _port = serverPort;
    }

    public void Start()
    {
        if (!IsRunning)
        {
            new Thread(ServerThreadProc)
                .Start();
        }
    }

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

    protected virtual void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            if (disposing)
            {
                // TODO: dispose managed state (managed objects)
            }

            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // TODO: set large fields to null
            IsDisposed = true;
        }
    }

    // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    // ~ModbusRtuServer()
    // {
    //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    //     Dispose(disposing: false);
    // }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
