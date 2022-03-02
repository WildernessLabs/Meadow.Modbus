using Meadow.Hardware;
using System;

namespace Meadow.Modbus
{
    public class SerialPortShim : ISerialPort, IDisposable
    {
        public event SerialDataReceivedEventHandler DataReceived = delegate { };
        public event EventHandler BufferOverrun = delegate { };

        private System.IO.Ports.SerialPort _port;
        private bool disposedValue;

        public int DataBits => _port.DataBits;
        public bool IsOpen => _port.IsOpen;
        public Parity Parity => (Parity)_port.Parity;
        public string PortName => _port.PortName;
        public int BytesToRead => _port.BytesToRead;
        public int ReceiveBufferSize => _port.ReadBufferSize;
        public StopBits StopBits => (StopBits)_port.StopBits - 1;

        public SerialPortShim(System.IO.Ports.SerialPort port)
        {
            _port = port;
        }

        public SerialPortShim(string portName, int baudRate, Parity parity, int dataBits, StopBits stopBits)
        {
            _port = new System.IO.Ports.SerialPort(portName, baudRate, (System.IO.Ports.Parity)parity, dataBits, (System.IO.Ports.StopBits)stopBits + 1);
        }

        public int BaudRate
        {
            get => _port.BaudRate;
            set => _port.BaudRate = value;
        }

        public TimeSpan ReadTimeout
        {
            get => TimeSpan.FromMilliseconds(_port.ReadTimeout);
            set => _port.ReadTimeout = (int)value.TotalMilliseconds;
        }

        public TimeSpan WriteTimeout
        {
            get => TimeSpan.FromMilliseconds(_port.WriteTimeout);
            set => _port.WriteTimeout = (int)value.TotalMilliseconds;
        }

        public void ClearReceiveBuffer()
        {
            _port.DiscardInBuffer();
        }

        public void Close()
        {
            _port.Close();
        }

        public void Open()
        {
            _port.Open();
        }

        public int Peek()
        {
            // TODO: not sure how to implement this without double-buffering, so skip for now
            throw new NotImplementedException();
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            return _port.Read(buffer, offset, count);
        }

        public int ReadAll(byte[] buffer)
        {
            buffer = new byte[_port.BytesToRead];
            _port.Read(buffer, 0, buffer.Length);
            return buffer.Length;
        }

        public int ReadByte()
        {
            return _port.ReadByte();
        }

        public int Write(byte[] buffer)
        {
            _port.Write(buffer, 0, buffer.Length);
            return buffer.Length;
        }

        public int Write(byte[] buffer, int offset, int count)
        {
            _port.Write(buffer, offset, buffer.Length);
            return count;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _port.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
