using Meadow.Hardware;
using System;
using System.Threading.Tasks;

namespace Meadow.Modbus
{
    public class CrcException : Exception
    {
        internal CrcException()
            : base("CRC Failure")
        {
        }
    }

    public class ModbusRtuClient : ModbusClientBase
    {
        private const int HEADER_DATA_OFFSET = 4;

        private ISerialPort _port;
        private IDigitalOutputPort? _enable;
        private double _byteTime;

        public string PortName => _port.PortName;

        public ModbusRtuClient(ISerialPort port, IDigitalOutputPort? enablePort = null)
        {
            _port = port;
            _enable = enablePort;
        }

        protected override void DisposeManagedResources()
        {
            _port?.Dispose();
        }

        private void SetEnable(bool state)
        {
            if (_enable != null)
            {
                _enable.State = state;
            }
        }

        protected Action? PostOpenAction { get; set; } = null;
        protected Action<byte[]>? PostWriteDelayAction { get; set; } = null;

        public override Task Connect()
        {
            SetEnable(false);

            if (!_port.IsOpen)
            {
                _port.Open();
                _port.ClearReceiveBuffer();

                PostOpenAction?.Invoke();
            }

            _byteTime = (1d / _port.BaudRate) * _port.DataBits * 1000d;

            IsConnected = true;
            return Task.CompletedTask;
        }

        public override void Disconnect()
        {
            _port?.Close();
            IsConnected = false;
        }

        protected override async Task<byte[]> ReadResult(ModbusFunction function)
        {
            // the response must be at least 5 bytes, so wait for at least that much to come in
            var t = 0;
            while (_port.BytesToRead < 5)
            {
                await Task.Delay(10);
                t += 10;
                if (_port.ReadTimeout.TotalMilliseconds > 0 && t > _port.ReadTimeout.TotalMilliseconds) throw new TimeoutException();
            }

            int headerLen = function switch
            {
                ModbusFunction.WriteMultipleRegisters => 6,
                _ => 3
            };

            var header = new byte[headerLen];

            // read header to determine result length
            _port.Read(header, 0, header.Length);

            int bufferLen;
            int resultLen;

            switch (function) // result function
            {
                // ref: https://www.modbustools.com/modbus.html

                case ModbusFunction.WriteMultipleRegisters:
                case ModbusFunction.WriteMultipleCoils: // Not implemented yet
                    bufferLen = 8; // fixed length
                    resultLen = 0; // no result data
                    break;
                case ModbusFunction.WriteCoil:
                    bufferLen = 8; // fixed length
                    resultLen = 0; // no result data
                    break;
                case ModbusFunction.WriteRegister:
                    bufferLen = 7 + header[headerLen - 1];
                    resultLen = header[2];
                    break;
                case ModbusFunction.ReadHoldingRegister:
                    bufferLen = 5 + header[headerLen - 1];
                    resultLen = header[2];
                    break;
                default:
                    bufferLen = 5 + header[headerLen - 1];
                    resultLen = header[2];
                    break;
            }

            var buffer = new byte[bufferLen]; // header + length + CRC

            // the CRC includes the header, so we need those in the buffer
            Array.Copy(header, buffer, headerLen);

            var read = headerLen;
            while (read < buffer.Length)
            {
                read += _port.Read(buffer, read, buffer.Length - read);
            }

            // do a CRC on all but the last 2 bytes, then see if that matches the last 2
            var expectedCrc = Crc(buffer, 0, buffer.Length - 2);
            var actualCrc = buffer[buffer.Length - 2] | buffer[buffer.Length - 1] << 8;
            if (expectedCrc != actualCrc) { throw new CrcException(); }

            if (resultLen == 0)
            {   //happens on write multiples
                return new byte[0];
            }

            var result = new byte[resultLen];
            Array.Copy(buffer, headerLen, result, 0, result.Length);

            return await Task.FromResult(result);
        }

        protected override Task DeliverMessage(byte[] message)
        {
            SetEnable(true);

            _port.Write(message);
            // the above call to the OS transfers data to the serial buffer - it does *not* mean all data has gone out on the wire
            // we must wait for all data to get transmitted before lowering the enable line

            PostWriteDelayAction?.Invoke(message);

            SetEnable(false);

            return Task.CompletedTask;
        }

        protected override byte[] GenerateReadMessage(byte modbusAddress, ModbusFunction function, ushort startRegister, int registerCount)
        {
            if (registerCount > ushort.MaxValue) throw new ArgumentException();

            var message = new byte[8]; // fn 3 is always 8 bytes

            message[0] = modbusAddress;
            message[1] = (byte)function;
            message[2] = (byte)(startRegister >> 8);
            message[3] = (byte)startRegister;
            message[4] = (byte)(registerCount >> 8);
            message[5] = (byte)registerCount;

            FillCRC(message);

            return message;

        }

        protected override byte[] GenerateWriteMessage(byte modbusAddress, ModbusFunction function, ushort register, byte[] data)
        {
            byte[] message;
            int offset = HEADER_DATA_OFFSET;

            switch (function)
            {
                case ModbusFunction.WriteMultipleCoils: // Not implemented yet
                    message = new byte[4 + data.Length + 2]; // header + length + data + crc
                    break;
                case ModbusFunction.WriteMultipleRegisters:
                    message = new byte[4 + data.Length + 5]; // header + length + data + crc
                    break;
                default:
                    message = new byte[4 + data.Length + 2]; // header + data + crc
                    break;
            }

            message[0] = modbusAddress;
            message[1] = (byte)function;
            message[2] = (byte)(register >> 8);
            message[3] = (byte)(register & 0xff);

            switch (function)
            {
                case ModbusFunction.WriteMultipleCoils: // Not implemented yet
                    break;
                case ModbusFunction.WriteMultipleRegisters:
                    var registers = (ushort)(data.Length / 2);
                    message[4] = (byte)(registers >> 8);
                    message[5] = (byte)(registers & 0xff);
                    message[6] = (byte)data.Length;
                    offset += 3;
                    break;
            }

            Array.Copy(data, 0, message, offset, data.Length);

            FillCRC(message);

            return message;
        }

        private ushort Crc(byte[] data, int index, int count)
        {
            ushort crc = 0xFFFF;
            char lsb;

            for (int i = index; i < count; i++)
            {
                crc = (ushort)(crc ^ data[i]);

                for (int j = 0; j < 8; j++)
                {
                    lsb = (char)(crc & 0x0001);
                    crc = (ushort)((crc >> 1) & 0x7fff);

                    if (lsb == 1)
                        crc = (ushort)(crc ^ 0xa001);
                }
            }

            return crc;
        }

        private void FillCRC(byte[] message)
        {
            var crc = Crc(message, 0, message.Length - 2);

            // fill in the CRC (last 2 bytes) - big-endian
            message[message.Length - 1] = (byte)((crc >> 8) & 0xff);
            message[message.Length - 2] = (byte)(crc & 0xff);
        }
    }
}
