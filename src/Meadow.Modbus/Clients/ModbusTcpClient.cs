using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Meadow.Modbus;

/// <summary>
/// Modbus TCP client implementation.
/// </summary>
public class ModbusTcpClient : ModbusClientBase, IDisposable
{
    /// <summary>
    /// The default Modbus TCP port (502).
    /// </summary>
    public const short DefaultModbusTCPPort = 502;

    /// <summary>
    /// Gets the destination IP address for the Modbus TCP client.
    /// </summary>
    public IPAddress Destination { get; private set; }

    /// <summary>
    /// Gets the port used for the Modbus TCP communication.
    /// </summary>
    public short Port { get; private set; }

    private TcpClient _client;
    private ushort _transaction = 0;
    private byte[] _responseBuffer = new byte[300]; // I think the max is 9 + 255, but this gives a little room

    /// <summary>
    /// Initializes a new instance of the <see cref="ModbusTcpClient"/> class using the specified destination address and port.
    /// </summary>
    /// <param name="destination">The destination address.</param>
    public ModbusTcpClient(IPEndPoint destination)
        : this(destination.Address, (short)destination.Port)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ModbusTcpClient"/> class using the specified destination address and port.
    /// </summary>
    /// <param name="destinationAddress">The destination address as a string.</param>
    /// <param name="port">The port to use for communication. Default is the Modbus TCP port (502).</param>
    public ModbusTcpClient(string destinationAddress, short port = DefaultModbusTCPPort)
        : this(IPAddress.Parse(destinationAddress), port)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ModbusTcpClient"/> class using the specified destination IP address and port.
    /// </summary>
    /// <param name="destination">The destination IP address.</param>
    /// <param name="port">The port to use for communication. Default is the Modbus TCP port (502).</param>
    public ModbusTcpClient(IPAddress destination, short port = DefaultModbusTCPPort)
    {
        Destination = destination;
        Port = port;
        _client = new TcpClient();
    }

    /// <inheritdoc/>
    protected override void DisposeManagedResources()
    {
        _client?.Dispose();
    }

    /// <summary>
    /// Connect to an endpoint. Allows for reusing the ModbusTcpClient.
    /// </summary>
    /// <param name="destination"></param>
    /// <returns></returns>
    public async Task Connect(IPEndPoint destination)
    {
        await Connect(destination.Address, (short)destination.Port);
    }

    /// <summary>
    /// Connect to an endpoint. Allows for reusing the ModbusTcpClient.
    /// </summary>
    /// <param name="destinationAddress"></param>
    /// <param name="port"></param>
    /// <returns></returns>
    public async Task Connect(string destinationAddress, short port = DefaultModbusTCPPort)
    {
        await Connect(IPAddress.Parse(destinationAddress), port);
    }

    /// <summary>
    /// Connect to an endpoint. Allows for reusing the ModbusTcpClient.
    /// </summary>
    /// <param name="destination"></param>
    /// <param name="port"></param>
    /// <returns></returns>
    public async Task Connect(IPAddress destination, short port = DefaultModbusTCPPort)
    {
        if (IsConnected)
            Disconnect();
        Destination = destination;
        Port = port;
        await Connect();
    }

    /// <inheritdoc/>
    public override async Task Connect()
    {
        try
        {
            if (_client == null || _client.Client == null)
            {
                _client = new TcpClient();
            }

            await _client.ConnectAsync(Destination, Port);

            IsConnected = _client.Connected;
            if (!IsConnected)
                throw new TimeoutException();

            _client.ReceiveTimeout = (int)Timeout.TotalMilliseconds;
            _client.SendTimeout = (int)Timeout.TotalMilliseconds;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Modbus TCP client cannot connect: {ex.Message}");
            IsConnected = false;
        }
    }

    /// <inheritdoc/>
    public override void Disconnect()
    {
        _client.Close();
    }

    /// <inheritdoc/>
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

        switch (function)
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

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    protected override async Task DeliverMessage(byte[] message)
    {
        if (Destination.Equals(IPAddress.None))
        {
            // this is used in testing, nothing gets sent
            return;
        }
        if (!_client.Connected)
        {
            return;
        }

        try
        {
            await _client.GetStream().WriteAsync(message, 0, message.Length);
        }
        catch
        {
            IsConnected = false;
            _client.Close();
        }
    }

    /// <inheritdoc/>
    protected override async Task<byte[]> ReadResult(ModbusFunction function)
    {
        if (Destination.Equals(IPAddress.None))
        {
            // this is used in testing, nothing gets sent
            switch (function)
            {
                case ModbusFunction.ReadHoldingRegister:
                    return new byte[125];
                default:
                    return new byte[0];
            }
        }

        if (!_client.Connected)
        {
            return new byte[0];
            //                throw new System.Net.Sockets.SocketException();
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
        try
        {
            count = await _client.GetStream().ReadAsync(_responseBuffer, 9, _responseBuffer[8]);
            // TODO: if count < the expected, we need to keep reading
        }
        catch
        {
            IsConnected = false;
            _client.Close();
            throw;
        }

        // if it's not an error, responseBuffer[8] is the payload length (as a byte)
        var result = new byte[_responseBuffer[8]];
        Array.Copy(_responseBuffer, 9, result, 0, result.Length);
        return result;
    }
}