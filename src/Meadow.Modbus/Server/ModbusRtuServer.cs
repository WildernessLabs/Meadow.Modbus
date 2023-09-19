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
        IModbusResult? result;
        RtuResponse? response;
        byte modbusAddress;

        while (!_signalStop)
        {
            read = 0;
            response = null;

            // all packets are > 4 bytes, this is enough to get the function and target address
            while (read < 4)
            {
                read += _port.Read(buffer, read, 4 - read);
            }

            modbusAddress = buffer[0];
            function = (ModbusFunction)buffer[1];

            switch (function)
            {
                case ModbusFunction.ReadHoldingRegister:
                    // always 8 bytes, read 4 more
                    while (read < 8)
                    {
                        read += _port.Read(buffer, read, 8 - read);
                    }

                    // TODO: check CRC (bytes 6&7)

                    result = ReadHoldingRegisterRequest?.Invoke(
                        buffer[0],
                        (ushort)((buffer[2] << 8) | buffer[3]),
                        (short)((buffer[4] << 8) | buffer[5]));

                    break;
                default:
                    result = new ModbusErrorResult(ModbusErrorCode.IllegalFunction);
                    break;
            }

            if (result is ModbusErrorResult mer)
            {
                response = RtuResponse.CreateErrorResponse(mer);
            }
            else if (result is ModbusReadResult mrr)
            {
                response = RtuResponse.CreateReadResponse(function, modbusAddress, mrr);
            }

            if (response != null)
            {
                var data = response.Serialize();
                _port.Write(data);
            }

            Thread.Sleep(1000);
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
