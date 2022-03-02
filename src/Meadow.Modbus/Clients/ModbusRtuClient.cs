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

        public override Task Connect()
        {
            SetEnable(false);

            if (!_port.IsOpen)
            {
                _port.Open();
                _port.ClearReceiveBuffer();
            }

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

            var header = new byte[3];

            // read 5 bytes so we can get the length
            _port.Read(header, 0, header.Length);

            // TODO: verify these?
            // header[0] == modbus address
            // header[1] == called function
            // header[2] == data length

            //            if (function != (ModbusFunction)header[1])
            //            {
            // TODO: should we care?
            //            }

            int dataLength;

            switch (function)
            {
                case ModbusFunction.WriteRegister:
                    dataLength = 7;
                    break;
                case ModbusFunction.ReadHoldingRegister:
                    dataLength = 5;
                    break;
                default:
                    dataLength = 5;
                    break;
            }
            var buffer = new byte[header[2] + dataLength]; // header + length + CRC

            // the CRC includes the header, so we need those in the buffer
            Array.Copy(header, buffer, 3);

            var read = 3;
            while (read < buffer.Length)
            {
                read += _port.Read(buffer, read, buffer.Length - read);
            }

            // do a CRC on all but the last 2 bytes, then see if that matches the last 2
            var expectedCrc = Crc(buffer, 0, buffer.Length - 2);
            var actualCrc = buffer[buffer.Length - 2] | buffer[buffer.Length - 1] << 8;
            if (expectedCrc != actualCrc) throw new CrcException();

            var result = new byte[buffer[2]];
            Array.Copy(buffer, 3, result, 0, result.Length);

            return await Task.FromResult(result);
        }

        protected override async Task DeliverMessage(byte[] message)
        {
            SetEnable(true);
            _port.Write(message);
            await Task.Delay(1); // without this delay, the CRC is unreliable. I suspect we're disabling before the destination has fully processed.  I'd love a < 1ms delay, but this is what we have
            SetEnable(false);
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
            var message = new byte[4 + data.Length + 2]; // header + data + crc

            message[0] = modbusAddress;
            message[1] = (byte)function;
            message[2] = (byte)(register >> 8);
            message[3] = (byte)(register & 0xff);

            Array.Copy(data, 0, message, HEADER_DATA_OFFSET, data.Length);

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
