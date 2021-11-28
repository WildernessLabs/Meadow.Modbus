using Meadow.Hardware;
using System;

namespace Meadow.Modbus.Unit.Tests
{
    public class MockSerialPort : ISerialPort
    {
        public int BaudRate { get; set; }

        public int BytesToRead => throw new NotImplementedException();

        public int DataBits => throw new NotImplementedException();

        public bool IsOpen { get; private set; }

        public Parity Parity => throw new NotImplementedException();

        public string PortName => throw new NotImplementedException();

        public int ReceiveBufferSize => throw new NotImplementedException();

        public int ReadTimeout { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public int WriteTimeout { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public StopBits StopBits => throw new NotImplementedException();

        public event SerialDataReceivedEventHandler DataReceived;
        public event EventHandler BufferOverrun;

        public byte[]? OutputBuffer { get; set; }

        public void ClearReceiveBuffer()
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