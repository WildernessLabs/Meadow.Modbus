using System;

namespace Meadow.Modbus;

public interface IModbusServer : IDisposable
{
    public event ReadDelegate? ReadCoilRequest;
    public event ReadDelegate? ReadDiscreteRequest;
    public event ReadDelegate? ReadHoldingRegisterRequest;
    public event ReadDelegate? ReadInputRegisterRequest;
    public event WriteCoilDelegate? WriteCoilRequest;

    void Start();
    void Stop();

    bool IsRunning { get; }
    bool IsDisposed { get; }
}
