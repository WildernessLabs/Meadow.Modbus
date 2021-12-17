using Meadow.Hardware;
using System;
using System.Threading.Tasks;

namespace Meadow.Modbus
{
    public class ModbusRtuClient : ModbusClientBase
    {
        private const int HEADER_DATA_OFFSET = 4;

        private ISerialPort _port;
        private IDigitalOutputPort? _enable;

        public ModbusRtuClient(ISerialPort port, IDigitalOutputPort? enablePort)
        {
            _port = port;
            _enable = enablePort;
        }

        private void SetEnable(bool state)
        {
            if (_enable != null)
            {
                Console.WriteLine($"{(state ? "ON" : "OFF")}");
                _enable.State = state;
            }
        }

        public override Task Connect()
        {
            SetEnable(false);

            if (!_port.IsOpen)
            {
                _port.Open();
            }
            IsConnected = true;
            return Task.CompletedTask;
        }

        public override void Disconnect()
        {
            _port?.Close();
            IsConnected = false;
        }

        protected override async Task<byte[]> ReadResult(ModbusFunction function, int expectedBytes)
        {
            // the response must be at least 5 bytes, so wait for at least that much to come in
            var t = 0;
            while(_port.BytesToRead < 5)
            {
                await Task.Delay(10);
                t += 10;
                if (t > _port.ReadTimeout) throw new TimeoutException();
            }

            var buffer = new byte[_port.BytesToRead];
            _port.Read(buffer, 0, buffer.Length);

            // do a CRC on all but the last 2 bytes, then see if that matches the last 2
            var expectedCrc = Crc(buffer, 0, buffer.Length - 2);
            var actualCrc = buffer[buffer.Length - 2] | buffer[buffer.Length - 1] << 8;
            if (expectedCrc != actualCrc) throw new Exception("CRC Failure");

            // TODO: verify there?
            // buffer[0] == modbus address
            // buffer[1] == called function
            // buffer[2] == data length

            if (function != (ModbusFunction)buffer[1])
            {
                // TODO: should we care?
            }

            var result = new byte[buffer[2]];
            Array.Copy(buffer, 3, result, 0, result.Length);

            return await Task.FromResult(result);
        }

        protected override Task DeliverMessage(byte[] message)
        {
            return Task.Run(async () =>
            {
                SetEnable(true);
                await Task.Delay(1);
                _port.Write(message);
                SetEnable(false);
            });
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
