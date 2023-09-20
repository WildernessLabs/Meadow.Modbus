using Meadow.Hardware;
using System;

namespace Meadow.Modbus;

public class SerialPortShim : ISerialPort, IDisposable
{
    /// <inheritdoc/>
    public event SerialDataReceivedEventHandler DataReceived = delegate { };
    /// <inheritdoc/>
    public event EventHandler BufferOverrun = delegate { };

    private System.IO.Ports.SerialPort _port;

    public bool IsDisposed { get; private set; }

    /// <inheritdoc/>
    public bool IsOpen => _port.IsOpen;

    /// <inheritdoc/>
    public string PortName => _port.PortName;

    /// <inheritdoc/>
    public int BytesToRead => (!IsOpen || IsDisposed) ? 0 : _port.BytesToRead;

    /// <inheritdoc/>
    public int ReceiveBufferSize => _port.ReadBufferSize;

    /// <inheritdoc/>
    public int DataBits
    {
        get => _port.DataBits;
        set => _port.DataBits = value;
    }

    /// <inheritdoc/>
    public Parity Parity
    {
        get => (Parity)_port.Parity;
        set => _port.Parity = (System.IO.Ports.Parity)value;
    }

    /// <inheritdoc/>
    public StopBits StopBits
    {
        get => (StopBits)_port.StopBits - 1;
        set => _port.StopBits = (System.IO.Ports.StopBits)value;
    }

    public SerialPortShim(System.IO.Ports.SerialPort port)
    {
        _port = port;
    }

    public SerialPortShim(string portName, int baudRate, Parity parity, int dataBits, StopBits stopBits)
    {
        _port = new System.IO.Ports.SerialPort(portName, baudRate, (System.IO.Ports.Parity)parity, dataBits, (System.IO.Ports.StopBits)stopBits + 1);
    }

    /// <inheritdoc/>
    public int BaudRate
    {
        get => _port.BaudRate;
        set => _port.BaudRate = value;
    }

    /// <inheritdoc/>
    public TimeSpan ReadTimeout
    {
        get => TimeSpan.FromMilliseconds(_port.ReadTimeout);
        set => _port.ReadTimeout = (int)value.TotalMilliseconds;
    }

    /// <inheritdoc/>
    public TimeSpan WriteTimeout
    {
        get => TimeSpan.FromMilliseconds(_port.WriteTimeout);
        set => _port.WriteTimeout = (int)value.TotalMilliseconds;
    }

    /// <inheritdoc/>
    public void ClearReceiveBuffer()
    {
        _port.DiscardInBuffer();
    }

    /// <inheritdoc/>
    public void Close()
    {
        if (_port.IsOpen)
        {
            _port.Close();
        }
    }

    /// <inheritdoc/>
    public void Open()
    {
        if (!_port.IsOpen)
        {
            _port.Open();
        }
    }

    /// <inheritdoc/>
    public int Peek()
    {
        // TODO: not sure how to implement this without double-buffering, so skip for now
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public int Read(byte[] buffer, int offset, int count)
    {
        return _port.Read(buffer, offset, count);
    }

    /// <inheritdoc/>
    public byte[] ReadAll()
    {
        var toRead = _port.BytesToRead;
        if (toRead == 0) return new byte[0];

        var buffer = new byte[_port.BytesToRead];
        var read = _port.Read(buffer, 0, buffer.Length);
        return buffer;
    }

    /// <inheritdoc/>
    public int ReadByte()
    {
        return _port.ReadByte();
    }

    /// <inheritdoc/>
    public int Write(byte[] buffer)
    {
        _port.Write(buffer, 0, buffer.Length);
        return buffer.Length;
    }

    /// <inheritdoc/>
    public int Write(byte[] buffer, int offset, int count)
    {
        _port.Write(buffer, offset, buffer.Length);
        return count;
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
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
