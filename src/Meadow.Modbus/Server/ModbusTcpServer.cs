using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Meadow.Modbus;
/// <summary>
/// Delegate for handling Modbus read requests.
/// </summary>
public delegate IModbusResult ReadDelegate(byte modbusAddress, ushort startRegister, short length);
/// <summary>
/// Delegate for handling Modbus write coil requests.
/// </summary>
public delegate IModbusResult WriteCoilDelegate(byte modbusAddress, ushort startRegister, bool[] data);
/// <summary>
/// Delegate for handling Modbus write register requests.
/// </summary>
public delegate IModbusResult WriteRegisterDelegate(byte modbusAddress, ushort startRegister, ushort[] data);

/// <summary>
/// Represents a Modbus TCP server.
/// </summary>
public class ModbusTcpServer : IModbusServer, IDisposable
{
    /// <summary>
    /// Event that is raised when a read coil request is received.
    /// </summary>
    public event ReadDelegate? ReadCoilRequest;

    /// <summary>
    /// Event that is raised when a read discrete request is received.
    /// </summary>
    public event ReadDelegate? ReadDiscreteRequest;

    /// <summary>
    /// Event that is raised when a read holding register request is received.
    /// </summary>
    public event ReadDelegate? ReadHoldingRegisterRequest;

    /// <summary>
    /// Event that is raised when a read input register request is received.
    /// </summary>
    public event ReadDelegate? ReadInputRegisterRequest;

    /// <summary>
    /// Event that is raised when a write coil request is received.
    /// </summary>
    public event WriteCoilDelegate? WriteCoilRequest;

    /// <summary>
    /// Event that is raised when a write register request is received.
    /// </summary>
    public event WriteRegisterDelegate? WriteRegisterRequest;

    /// <summary>
    /// Event that is raised when a client is connected to the server.
    /// </summary>
    public event EventHandler<EndPoint>? ClientConnected;

    /// <summary>
    /// Event that is raised when a client is disconnected from the server.
    /// </summary>
    public event EventHandler<EndPoint>? ClientDisconnected;

    /// <summary>
    /// The default Modbus TCP port number (502).
    /// </summary>
    public const int DefaultModbusTCPPort = 502;

    /// <summary>
    /// The default receive buffer size (1024 bytes).
    /// </summary>
    public const int DefaultReceiveBufferSize = 1024;

    private static int s_clientCount = 0;

    private TcpListener? _server = null;
    private readonly int _rxBufferSize;
    private bool _signalStop = false;

    /// <summary>
    /// Gets a value indicating whether the Modbus TCP server is disposed.
    /// </summary>
    public bool IsDisposed { get; private set; } = false;

    /// <summary>
    /// Gets the port number on which the Modbus TCP server is listening.
    /// </summary>
    public int Port { get; }

    /// <summary>
    /// Constructor for Modbus TCP server.
    /// </summary>
    /// <param name="port">Port number to listen on.</param>
    /// <param name="receiveBufferSize">Receive buffer size.</param>
    public ModbusTcpServer(int port = DefaultModbusTCPPort, int receiveBufferSize = DefaultReceiveBufferSize)
    {
        Port = port;

        if (receiveBufferSize <= 0) throw new ArgumentOutOfRangeException();

        _rxBufferSize = receiveBufferSize;
    }

    /// <summary>
    /// Disposes of the Modbus TCP server instance.
    /// </summary>
    public void Dispose()
    {
        IsDisposed = true;
        Stop();
    }

    /// <summary>
    /// Indicates whether the server is running.
    /// </summary>
    public bool IsRunning
    {
        get { return _server != null; }
    }

    /// <summary>
    /// Starts the Modbus TCP server.
    /// </summary>
    public void Start()
    {
        _server = new TcpListener(IPAddress.Any, Port);

        Task.Factory.StartNew(() => ServerThreadProc());
    }

    /// <summary>
    /// Stops the Modbus TCP server.
    /// </summary>
    public void Stop()
    {
        if (IsRunning)
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
                // Perform a blocking call to accept requests.
                if (_server != null)
                {
                    var client = await _server.AcceptTcpClientAsync();

                    // handle requests on their own tasks
                    _ = Task.Factory.StartNew(() => ClientHandlerProc(client));
                }
                else
                {
                    // unsure how this would ever happen, but preventing a compiler warning
                }
            }

            _server?.Stop();
            _server = null;
        }

        _signalStop = false;
    }

    private void ClientHandlerProc(TcpClient client)
    {
        var clientID = ++s_clientCount;

        ClientConnected?.Invoke(this, client.Client.RemoteEndPoint);

        // Buffer for reading data
        var rxBufferBytes = new byte[_rxBufferSize];

        // Get a stream object for reading and writing
        NetworkStream stream = client.GetStream();

        int i;

        // TODO: look for modbus packet delineation

        var bufferOffset = 0;
        var validDataLength = 0;

        try
        {
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

                    TcpResponse? response;
                    if (result is ModbusErrorResult mer)
                    {
                        response = TcpResponse.CreateErrorResponse(message.Function, header.TransactionID, header.UnitID, mer.ErrorCode);
                    }
                    else if (result is ModbusReadResult mrr)
                    {
                        response = TcpResponse.CreateReadResponse(message.Function, header.TransactionID, header.UnitID, mrr.Data);
                    }
                    else if (result is ModbusWriteResult mwr)
                    {
                        switch (message.Function)
                        {
                            case ModbusFunction.WriteCoil:
                                response = TcpResponse.CreateWriteCoilResponse(message.Function, header.TransactionID, header.UnitID, message.WriteCoilAddress, message.WriteCoilValue);
                                break;
                            case ModbusFunction.WriteRegister:
                                response = TcpResponse.CreateWriteRegisterResponse(message.Function, header.TransactionID, header.UnitID, message.WriteRegisterAddress, message.WriteRegisterValue);
                                break;
                            default:
                                response = TcpResponse.CreateWriteResponse(message.Function, header.TransactionID, header.UnitID, mwr.ItemsWritten);
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
                    }
                    else
                    {
                        Console.WriteLine($"MODBUS NULL RESPONSE");
                    }

                    bufferOffset = 0;
                    validDataLength = 0;
                }
            }
        }
        catch (System.IO.IOException)
        {
            // client likely disconnected
            ClientDisconnected?.Invoke(this, client.Client.RemoteEndPoint);
        }
        catch
        {
            if (IsDisposed) return;
            throw;
        }
        finally
        {
            // Shutdown and end connection
            client.Dispose();
        }
    }

    /// <summary>
    /// Processes the Modbus message and returns the result.
    /// </summary>
    /// <param name="message">The raw Modbus message.</param>
    /// <returns>The Modbus result after processing the message.</returns>
    private IModbusResult? ProcessMessage(RawMessage message)
    {
        switch (message.Function)
        {
            case ModbusFunction.ReadCoil:
                if (ReadCoilRequest != null)
                {
                    return ReadCoilRequest(255, message.ReadStart, message.ReadLength);
                }
                return null;
            case ModbusFunction.ReadDiscrete:
                if (ReadDiscreteRequest != null)
                {
                    return ReadDiscreteRequest(255, message.ReadStart, message.ReadLength);
                }
                return null;
            case ModbusFunction.ReadHoldingRegister:
                if (ReadHoldingRegisterRequest != null)
                {
                    return ReadHoldingRegisterRequest(255, message.ReadStart, message.ReadLength);
                }
                return null;
            case ModbusFunction.ReadInputRegister:
                if (ReadInputRegisterRequest != null)
                {
                    return ReadInputRegisterRequest(255, message.ReadStart, message.ReadLength);
                }
                return null;
            case ModbusFunction.WriteCoil:
                if (WriteCoilRequest != null)
                {
                    // incoming data is always 2 bytes, either 0x0000 or 0xffff
                    return WriteCoilRequest(255, message.WriteCoilAddress, new bool[] { message.WriteCoilValue });
                }
                return null;
            case ModbusFunction.WriteRegister:
                if (WriteRegisterRequest != null)
                {
                    return WriteRegisterRequest.Invoke(255, message.WriteRegisterAddress, new ushort[] { message.WriteRegisterValue });
                }
                return null;
            case ModbusFunction.WriteMultipleCoils:
                if (WriteCoilRequest != null && message.WriteCoilValues != null)
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

                    return WriteCoilRequest(255, message.WriteCoilAddress, data);
                }
                return null;
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

                    return WriteRegisterRequest(255, message.WriteRegisterAddress, data);
                }
                return null;
        }

        throw new ModbusException(ModbusErrorCode.IllegalFunction, message.Function);
    }
}
