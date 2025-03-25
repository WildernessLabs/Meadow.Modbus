using Meadow.Hardware;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Meadow.Modbus;

/// <summary>
/// Modbus RTU client implementation.
/// </summary>
public class ModbusRtuClient : ModbusClientBase
{
    private const int HEADER_DATA_OFFSET = 4;

    private readonly ISerialPort _port;
    private readonly IDigitalOutputPort? _enable;
    private readonly Stopwatch _stopwatch = new Stopwatch();

    /// <summary>
    /// Gets the name of the port used by the Modbus RTU client.
    /// </summary>
    public string PortName => _port.PortName;

    /// <summary>
    /// Gets or sets the action to be executed after the port is opened.
    /// </summary>
    protected Action? PostOpenAction { get; set; } = null;
    /// <summary>
    /// Gets or sets the action to be executed after a write delay.
    /// </summary>
    protected Action<byte[]>? PostWriteDelayAction { get; set; } = null;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModbusRtuClient"/> class.
    /// </summary>
    /// <param name="port">The serial port to use for communication.</param>
    /// <param name="timeout">The timeout period for communications</param>
    public ModbusRtuClient(ISerialPort port, TimeSpan timeout)
    {
        _port = port;
        Timeout = timeout;

        port.ReadTimeout = port.WriteTimeout = Timeout;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ModbusRtuClient"/> class.
    /// </summary>
    /// <param name="port">The serial port to use for communication.</param>
    /// <param name="enablePort">The optional digital output port for enabling communication.</param>
    public ModbusRtuClient(ISerialPort port, IDigitalOutputPort? enablePort = null)
        : this(port, TimeSpan.FromSeconds(5))
    {
        _enable = enablePort;
    }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public override Task Connect()
    {
        SetEnable(false);

        if (!_port.IsOpen)
        {
            _port.Open();
            _port.ClearReceiveBuffer();

            PostOpenAction?.Invoke();
        }

        IsConnected = true;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override void Disconnect()
    {
        _port?.Close();
        IsConnected = false;
    }

    /// <inheritdoc/>
    protected override async Task<byte[]> ReadResult(ModbusFunction function)
    {
        // the response must be at least 5 bytes, so wait for at least that much to come in
        _stopwatch.Restart();
        try
        {
            ushort expectedCrc;
            ushort actualCrc;

            while (_port.BytesToRead < 5)
            {
                if ((Timeout.TotalMilliseconds > 0) && (_stopwatch.ElapsedMilliseconds > Timeout.TotalMilliseconds))
                {
                    _port.ClearReceiveBuffer();
                    throw new TimeoutException();
                }
                await Task.Delay(10);
            }

            // Standard header size for all functions is 3 bytes
            const int headerLen = 3;
            var header = new byte[headerLen];

            // Read the standard 3-byte header (address, function code, data length/start)
            var read = 0;
            while (read < headerLen)
            {
                if (_stopwatch.ElapsedMilliseconds > Timeout.TotalMilliseconds)
                {
                    _port.ClearReceiveBuffer();
                    throw new TimeoutException();
                }
                read += _port.Read(header, read, headerLen - read);
            }

            // Check for an error bit (MSB in byte 2)
            if ((header[1] & 0x80) != 0)
            {
                // an error response has come back - read the remaining 2 bytes (CRC of the error)
                var errpacket = new byte[5];
                Array.Copy(header, 0, errpacket, 0, 3);

                read = 0;
                while (read < 2)
                {
                    if (_stopwatch.ElapsedMilliseconds > Timeout.TotalMilliseconds)
                    {
                        _port.ClearReceiveBuffer();
                        throw new TimeoutException();
                    }
                    read += _port.Read(errpacket, 3 + read, 2 - read);
                }

                var errorCode = (ModbusErrorCode)errpacket[2];

                expectedCrc = RtuHelpers.Crc(errpacket, 0, errpacket.Length - 2);
                actualCrc = (ushort)(errpacket[errpacket.Length - 2] | errpacket[errpacket.Length - 1] << 8);

                _port.ClearReceiveBuffer();

                if (expectedCrc != actualCrc)
                {
                    throw new CrcException($"CRC error in {errorCode} message", expectedCrc, actualCrc, errpacket);
                }

                throw new ModbusException(errorCode, function);
            }

            int bufferLen;
            int resultLen;

            // Determine total message length and result data length based on function code
            switch (function)
            {
                case ModbusFunction.WriteRegister:
                case ModbusFunction.WriteCoil:
                case ModbusFunction.WriteMultipleCoils:
                case ModbusFunction.WriteMultipleRegisters:
                    // Fixed-length responses: addr + func + 2 addr + 2 value/count + 2 CRC = 8 bytes
                    bufferLen = 8;
                    resultLen = 0; // No data payload to extract
                    break;

                case ModbusFunction.ReadHoldingRegister:
                case ModbusFunction.ReadInputRegister:
                case ModbusFunction.ReadCoil:
                case ModbusFunction.ReadDiscrete:
                    // Variable-length responses: length is in the 3rd byte of header
                    bufferLen = 3 + header[2] + 2; // addr + func + len + data + CRC
                    resultLen = header[2];
                    break;

                case ModbusFunction.ReportId:
                    // Special case for ReportId which has a run indicator byte before CRC
                    bufferLen = 3 + header[2] + 1 + 2; // addr + func + len + data + run indicator + CRC
                    resultLen = header[2];
                    break;

                default:
                    // General case for other functions
                    bufferLen = 3 + header[2] + 2; // addr + func + len + data + CRC
                    resultLen = header[2];
                    break;
            }

            var buffer = new byte[bufferLen];

            // Copy the header we already read
            Array.Copy(header, buffer, headerLen);

            // Read the rest of the message
            read = headerLen;
            while (read < buffer.Length)
            {
                if (_stopwatch.ElapsedMilliseconds > Timeout.TotalMilliseconds)
                {
                    _port.ClearReceiveBuffer();
                    throw new TimeoutException();
                }
                read += _port.Read(buffer, read, buffer.Length - read);
            }

            // Verify CRC
            expectedCrc = RtuHelpers.Crc(buffer, 0, buffer.Length - 2);
            actualCrc = (ushort)(buffer[buffer.Length - 2] | buffer[buffer.Length - 1] << 8);

            _port.ClearReceiveBuffer();

            if (expectedCrc != actualCrc)
            {
                throw new CrcException("CRC error in response message", expectedCrc, actualCrc, buffer);
            }

            if (resultLen == 0)
            {
                // No data to extract (write operations)
                return new byte[0];
            }

            // Extract the result data
            var result = new byte[resultLen];
            Array.Copy(buffer, headerLen, result, 0, resultLen);

            return result;
        }
        finally
        {
            _stopwatch.Stop();
        }
    }

    /// <inheritdoc/>
    protected override Task DeliverMessage(byte[] message)
    {
        SetEnable(true);

        // Clear the recieve buffer. if a pervious request timed out but a message was still recieved it will now be in the recieve buffer.
        // This can happen if using modbus over zigbee (or any other radio/mesh protocol) or is the timeout is too short 
        _port.ClearReceiveBuffer();

        _port.Write(message);
        // the above call to the OS transfers data to the serial buffer - it does *not* mean all data has gone out on the wire
        // we must wait for all data to get transmitted before lowering the enable line

        PostWriteDelayAction?.Invoke(message);

        SetEnable(false);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    protected override byte[] GenerateReportMessage(byte modbusAddress)
    {
        var message = new byte[4]; // fn 0x11 is always 4 bytes
        message[0] = modbusAddress;
        message[1] = (byte)0x11;
        RtuHelpers.FillCRC(message);

        return message;
    }

    /// <inheritdoc/>
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

        RtuHelpers.FillCRC(message);

        return message;

    }

    /// <inheritdoc/>
    protected override byte[] GenerateWriteMessage(byte modbusAddress, ModbusFunction function, ushort register, byte[] data)
    {
        byte[] message;
        int offset = HEADER_DATA_OFFSET;

        switch (function)
        {
            case ModbusFunction.WriteMultipleCoils:
                message = new byte[4 + data.Length + 2]; // header + length + crc
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
            case ModbusFunction.WriteMultipleCoils:
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

        RtuHelpers.FillCRC(message);

        return message;
    }
}
