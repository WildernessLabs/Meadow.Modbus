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

    private ISerialPort _port;
    private IDigitalOutputPort? _enable;
    private Stopwatch _stopwatch = new Stopwatch();

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

            int headerLen = function switch
            {
                ModbusFunction.WriteMultipleRegisters => 6,
                _ => 3
            };

            var header = new byte[headerLen];

            // first read 3 bytes so we can look for an error
            var read = 0;
            while (read < 3)
            {
                if (_stopwatch.ElapsedMilliseconds > Timeout.TotalMilliseconds)
                {
                    _port.ClearReceiveBuffer();
                    throw new TimeoutException();
                }
                read += _port.Read(header, read, 3 - read);
            }

            // check for an error bit (MSB in byte 2)
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

            // read the remainder of the header
            if (headerLen > 3)
            {
                read = 0;
                while (read < headerLen - 2)
                {
                    if (_stopwatch.ElapsedMilliseconds > Timeout.TotalMilliseconds)
                    {
                        _port.ClearReceiveBuffer();
                        throw new TimeoutException();
                    }
                    read += _port.Read(header, 3, headerLen - 3);
                }
            }

            int bufferLen;
            int resultLen;

            switch (function)
            {
                case ModbusFunction.WriteRegister:
                case ModbusFunction.WriteMultipleCoils:
                case ModbusFunction.WriteCoil:
                    bufferLen = 8; //fixed length
                    resultLen = 0; //no result data
                    break;
                case ModbusFunction.WriteMultipleRegisters:
                    bufferLen = 7 + header[headerLen - 1];
                    resultLen = header[2];
                    break;
                case ModbusFunction.ReadHoldingRegister:
                    bufferLen = 5 + header[headerLen - 1];
                    resultLen = header[2];
                    break;
                case ModbusFunction.ReportId:
                    bufferLen = 5 + header[headerLen - 1] + 1; // run indicator byte right before the CRC
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

            // do a CRC on all but the last 2 bytes, then see if that matches the last 2
            expectedCrc = RtuHelpers.Crc(buffer, 0, buffer.Length - 2);
            actualCrc = (ushort)(buffer[buffer.Length - 2] | buffer[buffer.Length - 1] << 8);

            _port.ClearReceiveBuffer();

            if (expectedCrc != actualCrc)
            {
                throw new CrcException("CRC error in response message", expectedCrc, actualCrc, buffer);
            }

            if (resultLen == 0)
            {   //happens on write multiples
                return new byte[0];
            }

            var result = new byte[resultLen];
            Array.Copy(buffer, headerLen, result, 0, result.Length);

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
