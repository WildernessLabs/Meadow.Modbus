using Meadow.Hardware;
using System;

namespace Meadow.Modbus.Unit.Tests
{
    public class MockSerialPort : ISerialPort
    {
        public int BaudRate { get; set; }

        public int BytesToRead => ReceiveBuffer?.Length ?? 0;

        public int DataBits => 8;

        public bool IsOpen { get; private set; }

        public Parity Parity => Parity.None;

        public string PortName => "SIM";

        public int ReceiveBufferSize => throw new NotImplementedException();

        public TimeSpan ReadTimeout { get; set; } = TimeSpan.FromMilliseconds(100);
        public TimeSpan WriteTimeout { get; set; } = TimeSpan.FromMilliseconds(100);

        public StopBits StopBits => StopBits.One;

        public event SerialDataReceivedEventHandler DataReceived;
        public event EventHandler BufferOverrun;

        public byte[]? OutputBuffer { get; set; }
        public byte[]? ReceiveBuffer { get; set; }

        public void ClearReceiveBuffer()
        {
        }

        public void Dispose()
        {
        }

        public void Close()
        {
            IsOpen = false;
        }

        public void Open()
        {
            IsOpen = true;
        }

        public int Peek()
        {
            throw new NotImplementedException();
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public int ReadAll(byte[] buffer)
        {
            throw new NotImplementedException();
        }

        public int ReadByte()
        {
            throw new NotImplementedException();
        }

        public int Write(byte[] buffer)
        {
            OutputBuffer = buffer;
            return buffer.Length;
        }

        public int Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }
    }
}