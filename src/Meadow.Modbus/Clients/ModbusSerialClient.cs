using Meadow.Hardware;
using System;
using System.Threading.Tasks;

namespace Meadow.Modbus
{
    public class ModbusSerialClient : ModbusClientBase
    {
        private const int HEADER_DATA_OFFSET = 4;

        private ISerialPort _port;

        public ModbusSerialClient(ISerialPort port)
        {
            _port = port;
        }

        public override Task Connect()
        {
            _port.Open();
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
            return null;
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

        protected override Task DeliverMessage(byte[] message)
        {
            _port.Write(message);
            return Task.CompletedTask;
        }

        private void FillCRC(byte[] message)
        {
            ushort crc = 0xFFFF;
            char lsb;

            for (int i = 0; i < (message.Length) - 2; i++)
            {
                crc = (ushort)(crc ^ message[i]);

                for (int j = 0; j < 8; j++)
                {
                    lsb = (char)(crc & 0x0001);
                    crc = (ushort)((crc >> 1) & 0x7fff);

                    if (lsb == 1)
                        crc = (ushort)(crc ^ 0xa001);
                }
            }

            // fill in the CRC (last 2 bytes) - big-endian
            message[message.Length - 1] = (byte)((crc >> 8) & 0xff);
            message[message.Length - 2] = (byte)(crc & 0xff);
        }
    }
}
