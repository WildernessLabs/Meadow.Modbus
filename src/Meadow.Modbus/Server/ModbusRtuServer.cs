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
                        result = WriteRegisterRequest(
                        buffer[0],
                        (ushort)((buffer[2] << 8) | buffer[3]),
                        new ushort[]
                        {
                            (ushort)((buffer[4] << 8) | buffer[5]),
                        });
                    }
                    break;
                case ModbusFunction.WriteMultipleRegisters:
                    break;
                default:
                    result = new ModbusErrorResult(ModbusErrorCode.IllegalFunction);
                    break;
            }

            if (result is ModbusReadResult mrr)
            {
                response = RtuResponse.CreateReadResponse(function, modbusAddress, mrr);
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
