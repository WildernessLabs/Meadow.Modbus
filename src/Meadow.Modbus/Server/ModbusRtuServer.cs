using System;

namespace Meadow.Modbus;

public class ModbusRtuServer : IModbusServer
{
    public bool IsDisposed { get; private set; }

    public event ReadDelegate? ReadCoilRequest;
    public event ReadDelegate? ReadDiscreteRequest;
    public event ReadDelegate? ReadHoldingRegisterRequest;
    public event ReadDelegate? ReadInputRegisterRequest;
    public event WriteCoilDelegate? WriteCoilRequest;
    public event WriteRegisterDelegate? WriteRegisterRequest;

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
