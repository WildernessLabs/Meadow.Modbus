using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Meadow.Modbus
{
    public class ModbusTcpClient : ModbusClientBase, IDisposable
    {
        public const short DefaultModbusTCPPort = 502;

        public IPAddress Destination { get; }
        public short Port { get; }

        private readonly TcpClient _client;
        private ushort _transaction = 0;
        private byte[] _responseBuffer = new byte[300]; // I think the max is 9 + 255, but this gives a little room

        public ModbusTcpClient(string destinationAddress, short port = DefaultModbusTCPPort)
            : this(IPAddress.Parse(destinationAddress), port)
        {
        }

        public ModbusTcpClient(IPAddress destination, short port = DefaultModbusTCPPort)
        {
            Destination = destination;
            Port = port;
            _client = new TcpClient();
        }

        protected override void DisposeManagedResources()
        {
            _client?.Dispose();
        }

        public override async Task Connect()
        {
            await _client.ConnectAsync(Destination, Port);
        }

        public override void Disconnect()
        {
            _client.Close();
        }

        protected override byte[] GenerateWriteMessage(byte modbusAddress, ModbusFunction function, ushort register, byte[] data)
        {
            // single and multiples are slightly different
            var headerSize = function switch
            {
                ModbusFunction.WriteCoil => 10,
                ModbusFunction.WriteRegister => 10,
                ModbusFunction.WriteMultipleCoils => 13,
                ModbusFunction.WriteMultipleRegisters => 13,
                _ => throw new ArgumentException("function is not a write")
            };

            var registerCount = data.Length / 2;

            var message = new byte[headerSize + data.Length]; // header + data

            // transaction ID
            _transaction++;
            message[0] = (byte)(_transaction >> 8);
            message[1] = (byte)(_transaction & 0xff); ;

            // protocol (0 == TCP)
            message[2] = 0;
            message[3] = 0;

            var size = (ushort)(5 + registerCount);
            message[4] = (byte)(size >> 8);
            message[5] = (byte)(size & 0xff);

            message[6] = modbusAddress;
            message[7] = (byte)function;

            message[8] = (byte)(register >> 8);
            message[9] = (byte)(register & 0xff);

            Array.Copy(data, 0, message, headerSize, data.Length);

            switch(function)
            {
                case ModbusFunction.WriteMultipleCoils:
                case ModbusFunction.WriteMultipleRegisters:
                    // TODO: this is an item count, and is almost certainly wrong right now
                    var itemCount = IPAddress.HostToNetworkOrder((short)data.Length);
                    data[10] = (byte)(itemCount >> 8);
                    data[11] = (byte)(itemCount & 0xff);
                    data[12] = (byte)(data.Length - 2);
                    break;
            }

            // Unlike RTU, Modbus TCP doesn't do a CRC - it relies on TCP/IP to do error checking

            return message;
        }

        protected override byte[] GenerateReadMessage(byte modbusAddress, ModbusFunction function, ushort startRegister, int registerCount)
        {
            if (registerCount > ushort.MaxValue) throw new ArgumentException();

            var message = new byte[12];

            // transaction ID
            _transaction++;
            message[0] = (byte)(_transaction >> 8);
            message[1] = (byte)(_transaction & 0xff); ;

            // protocol (0 == TCP)
            message[2] = 0;
            message[3] = 0;

            // field length (bytes)
            message[4] = 0; // fixed size for a read - no need to calculate
            message[5] = 6;

            message[6] = modbusAddress;
            message[7] = (byte)function;

            message[8] = (byte)(startRegister >> 8);
            message[9] = (byte)(startRegister & 0xff);

            message[10] = (byte)(registerCount >> 8);
            message[11] = (byte)(registerCount & 0xff);

            return message;
        }

        protected override async Task DeliverMessage(byte[] message)
        {
            if (Destination.Equals(IPAddress.None))
            {
                // this is used in testing, nothing gets sent
                return;
            }

            await _client.GetStream().WriteAsync(message, 0, message.Length);

        }

        protected override async Task<byte[]> ReadResult(ModbusFunction function)
        {
            if (Destination.Equals(IPAddress.None))
            {
                // this is used in testing, nothing gets sent
                switch(function)
                {
                    case ModbusFunction.ReadHoldingRegister:
                        return new byte[125];
                    default:
                        return new byte[0];
                }
            }

            // responses (even an error) are at least 9 bytes - read enough to know the status
            Array.Clear(_responseBuffer, 0, _responseBuffer.Length);
            var count = await _client.GetStream().ReadAsync(_responseBuffer, 0, 9);

            // TODO: we assume we get 9 bytes back here - handle that *not* happening

            if ((_responseBuffer[7] & 0x80) != 0)
            {
                // we have an error
                var reason = (ModbusErrorCode)_responseBuffer[8];
                throw new ModbusException(reason, function);
            }

            // read any remaining payload
            count = await _client.GetStream().ReadAsync(_responseBuffer, 9, _responseBuffer[8]);

            // TODO: if count < the expected, we need to keep reading

            // if it's not an error, responseBuffer[8] is the payload length (as a byte)
            var result = new byte[_responseBuffer[8]];
            Array.Copy(_responseBuffer, 9, result, 0, result.Length);
            return result;
        }
    }
}
