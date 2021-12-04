using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Meadow.Modbus
{
    public delegate IModbusResult ReadDelegate(ushort startAddress, short length);
    public delegate IModbusResult WriteCoilDelegate(ushort startAddress, bool[] data);
    public delegate IModbusResult WriteRegisterDelegate(ushort startAddress, ushort[] data);

    public class ModbusTcpServer : IDisposable
    {
        public event ReadDelegate? ReadCoilRequest;
        public event ReadDelegate? ReadDiscreteRequest;
        public event ReadDelegate? ReadHoldingRegisterRequest;
        public event ReadDelegate? ReadInputRegisterRequest;
        public event WriteCoilDelegate? WriteCoilRequest;
        public event WriteRegisterDelegate? WriteRegisterRequest;

        public const int DefaultModbusTCPPort = 502;
        public const int DefaultReceiveBufferSize = 1024;

        private static int s_clientCount = 0;

        private TcpListener? _server = null;
        private readonly int _rxBufferSize;
        private bool _signalStop = false;

        public int Port { get; }

        public ModbusTcpServer(int port = DefaultModbusTCPPort, int receiveBufferSize = DefaultReceiveBufferSize)
        {
            Port = port;

            if (receiveBufferSize <= 0) throw new ArgumentOutOfRangeException();

            _rxBufferSize = receiveBufferSize;
        }

        public void Dispose()
        {
            if (_server != null)
            {
                _server.Stop();
            }
        }

        public bool IsRunning
        {
            get { return _server != null; }
        }

        public void Start()
        {
            _server = new TcpListener(IPAddress.Any, Port);

            Task.Factory.StartNew(() => ServerThreadProc());
        }

        public void Stop()
        {
            if(IsRunning)
            {
                _signalStop = true;
            }
        }

        private async Task ServerThreadProc()
        {
            // Start listening for client requests.
            if (_server != null)
            {
                _server?.Start();

                while (!_signalStop)
                {
                    Debug.Write("Waiting for a connection... ");

                    // Perform a blocking call to accept requests.
                    var client = await _server.AcceptTcpClientAsync();

                    // handle requests on their own tasks
                    _ = Task.Factory.StartNew(() => ClientHandlerProc(client));
                }

                _server?.Stop();
                _server = null;
            }

            _signalStop = false;
        }

        private void ClientHandlerProc(TcpClient client)
        {
            var clientID = ++s_clientCount;

            Debug.WriteLine($"Modbus TCP Client {clientID} Connected!");

            // Buffer for reading data
            var rxBufferBytes = new byte[_rxBufferSize];

            // Get a stream object for reading and writing
            NetworkStream stream = client.GetStream();

            int i;

            // TODO: look for modbus packet delineation

            var bufferOffset = 0;
            var validDataLength = 0;

            while ((i = stream.Read(rxBufferBytes, bufferOffset, rxBufferBytes.Length)) != 0)
            {
                // the modbus header is a minimum of 7 bytes, so if it's less we need to keep waiting
                validDataLength += i;

                if (i < ModbusTcpHeader.Length)
                {
                    bufferOffset += i;
                }
                else
                {
                    // the header is in network host order (i.e. big-endian) so we need to do some swapping of stuff to read it
                    var header = new ModbusTcpHeader(rxBufferBytes, bufferOffset);

                    // Process the data sent by the client.

                    RawMessage? message;
                    if (header.DataLength > validDataLength)
                    {
                        // we need to read more data from the wire
                        // TODO: add handling for this
                        throw new Exception("Data starved.");
                    }
                    else
                    {
                        message = new RawMessage(rxBufferBytes, 0 + ModbusTcpHeader.Length, validDataLength - ModbusTcpHeader.Length);
                    }

                    var result = ProcessMessage(message);

                    Response? response;
                    if (result is ModbusErrorResult mer)
                    {
                        response = Response.CreateErrorResponse(message.Function, header.TransactionID, header.UnitID, mer.ErrorCode);
                    }
                    else if (result is ModbusReadResult mrr)
                    {
                        response = Response.CreateReadResponse(message.Function, header.TransactionID, header.UnitID, mrr.Data);
                    }
                    else if (result is ModbusWriteResult mwr)
                    {
                        switch (message.Function)
                        {
                            case ModbusFunction.WriteCoil:
                                response = Response.CreateWriteCoilResponse(message.Function, header.TransactionID, header.UnitID, message.WriteCoilAddress, message.WriteCoilValue);
                                break;
                            case ModbusFunction.WriteRegister:
                                response = Response.CreateWriteRegisterResponse(message.Function, header.TransactionID, header.UnitID, message.WriteRegisterAddress, message.WriteRegisterValue);
                                break;
                            default:
                                response = Response.CreateWriteResponse(message.Function, header.TransactionID, header.UnitID, mwr.ItemsWritten);
                                break;
                        }
                    }
                    else
                    {
                        throw new Exception("Invalid Modbus result");
                    }

                    if (response != null)
                    {
                        // Send back a response.
                        var data = response.Serialize();
                        stream.Write(data, 0, data.Length);
                        Debug.WriteLine($"Modbus Client {clientID}:Sent: {data.Length} bytes");
                    }

                    bufferOffset = 0;
                    validDataLength = 0;
                }
            }

            // Shutdown and end connection
            client.Dispose();
        }

        private IModbusResult ProcessMessage(RawMessage message)
        {
            switch (message.Function)
            {
                case ModbusFunction.ReadCoil:
                    if (ReadCoilRequest != null)
                    {
                        return ReadCoilRequest(message.ReadStart, message.ReadLength);
                    }
                    break;
                case ModbusFunction.ReadDiscrete:
                    if (ReadDiscreteRequest != null)
                    {
                        return ReadDiscreteRequest(message.ReadStart, message.ReadLength);
                    }
                    break;
                case ModbusFunction.ReadHoldingRegister:
                    if (ReadHoldingRegisterRequest != null)
                    {
                        return ReadHoldingRegisterRequest(message.ReadStart, message.ReadLength);
                    }
                    break;
                case ModbusFunction.ReadInputRegister:
                    if (ReadInputRegisterRequest != null)
                    {
                        return ReadInputRegisterRequest(message.ReadStart, message.ReadLength);
                    }
                    break;
                case ModbusFunction.WriteCoil:
                    if (WriteCoilRequest != null)
                    {
                        // incoming data is always 2 bytes, either 0x0000 or 0xffff
                        return WriteCoilRequest(message.WriteCoilAddress, new bool[] { message.WriteCoilValue });
                    }
                    break;
                case ModbusFunction.WriteRegister:
                    return WriteRegisterRequest(message.WriteRegisterAddress, new ushort[] { message.WriteRegisterValue });
                case ModbusFunction.WriteMultipleCoils:
                    if (WriteCoilRequest != null)
                    {
                        var index = 0;
                        var bit = 0;
                        var data = new bool[message.WriteCoilCount];
                        var v = false;
                        for (var i = 0; i < message.WriteCoilCount; i++)
                        {
                            v = (message.WriteCoilValues[index] & (1 << bit)) != 0;
                            if (++bit > 7)
                            {
                                bit = 0;
                                index++;
                            }
                            data[i] = v;
                        }

                        return WriteCoilRequest(message.WriteCoilAddress, data);
                    }
                    break;
                case ModbusFunction.WriteMultipleRegisters:
                    if (WriteRegisterRequest != null)
                    {
                        var index = 0;
                        var data = new ushort[message.WriteRegisterCount];
                        short s = 0;
                        for (var i = 0; i < message.WriteRegisterCount; i++)
                        {
                            s = IPAddress.NetworkToHostOrder(BitConverter.ToInt16(message.WriteRegisterValues, index));
                            index += 2;
                            data[i] = (ushort)s;
                        }

                        return WriteRegisterRequest(message.WriteRegisterAddress, data);
                    }
                    break;
            }

            throw new ModbusException(ModbusErrorCode.IllegalFunction, message.Function);
        }
    }
}
