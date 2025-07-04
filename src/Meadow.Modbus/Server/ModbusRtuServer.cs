using Meadow.Hardware;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Meadow.Modbus;

/// <summary>
/// Represents an improved RTU server with better threading, error handling, and resource management.
/// </summary>
public class ModbusRtuServer : IModbusServer
{
    private readonly ISerialPort _port;
    private volatile bool _signalStop;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _serverTask;
    private readonly object _lockObject = new object();

    // Configuration
    private readonly int _receiveTimeoutMs;
    private readonly int _interFrameDelayMs;
    private readonly int _maxFrameSize;

#pragma warning disable 414 // disable "assigned but never used" warning for events not yet supported by the RTU server
    /// <inheritdoc/>
    public event ReadDelegate? ReadCoilRequest = default!;
    /// <inheritdoc/>
    public event ReadDelegate? ReadDiscreteRequest = default!;
    /// <inheritdoc/>
    public event ReadDelegate? ReadHoldingRegisterRequest = default!;
    /// <inheritdoc/>
    public event ReadDelegate? ReadInputRegisterRequest = default!;
    /// <inheritdoc/>
    public event WriteCoilDelegate? WriteCoilRequest = default!;
    /// <inheritdoc/>
    public event WriteRegisterDelegate? WriteRegisterRequest;

    /// <summary>
    /// Event that is raised when a CRC (Cyclic Redundancy Check) error is detected.
    /// </summary>
    public event EventHandler? CrcErrorDetected;

    /// <summary>
    /// Event that is raised when a communication error occurs.
    /// </summary>
    public event EventHandler<string>? CommunicationError;

    /// <summary>
    /// Event that is raised when a frame is received (for diagnostics).
    /// </summary>
    public event EventHandler<byte[]>? FrameReceived;
#pragma warning restore 414

    /// <inheritdoc/>
    public bool IsDisposed { get; private set; }

    /// <inheritdoc/>
    public bool IsRunning
    {
        get
        {
            lock (_lockObject)
            {
                return _serverTask != null && !_serverTask.IsCompleted && _port.IsOpen;
            }
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ModbusRtuServer"/> class using the specified serial port.
    /// </summary>
    /// <param name="serverPort">The serial port to use for communication.</param>
    /// <param name="receiveTimeoutMs">Timeout in milliseconds for receiving data (default: 1000ms).</param>
    /// <param name="interFrameDelayMs">Inter-frame delay in milliseconds (default: 10ms).</param>
    /// <param name="maxFrameSize">Maximum frame size in bytes (default: 256).</param>
    public ModbusRtuServer(ISerialPort serverPort, int receiveTimeoutMs = 1000, int interFrameDelayMs = 10, int maxFrameSize = 256)
    {
        _port = serverPort ?? throw new ArgumentNullException(nameof(serverPort));
        _receiveTimeoutMs = receiveTimeoutMs;
        _interFrameDelayMs = interFrameDelayMs;
        _maxFrameSize = maxFrameSize;
    }

    /// <inheritdoc/>
    public void Start()
    {
        lock (_lockObject)
        {
            if (IsRunning || IsDisposed)
                return;

            _signalStop = false;
            _cancellationTokenSource = new CancellationTokenSource();
            _serverTask = Task.Run(() => ServerThreadProc(_cancellationTokenSource.Token));
        }
    }

    /// <inheritdoc/>
    public void Stop()
    {
        lock (_lockObject)
        {
            if (!IsRunning)
                return;

            _signalStop = true;
            _cancellationTokenSource?.Cancel();

            try
            {
                _serverTask?.Wait(5000); // Wait up to 5 seconds for graceful shutdown
            }
            catch (AggregateException ex) when (ex.InnerException is OperationCanceledException)
            {
                // Expected when cancellation is requested
            }
            catch (Exception ex)
            {
                CommunicationError?.Invoke(this, $"Error during server shutdown: {ex.Message}");
            }

            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            _serverTask = null;
        }
    }

    private async Task ServerThreadProc(CancellationToken cancellationToken)
    {
        try
        {
            if (!_port.IsOpen)
            {
                _port.Open();
            }

            var buffer = new byte[_maxFrameSize];

            while (!_signalStop && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var frame = await ReceiveFrame(buffer, cancellationToken);
                    if (frame != null && frame.Length > 0)
                    {
                        FrameReceived?.Invoke(this, frame);
                        await ProcessFrame(frame, cancellationToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    break; // Normal shutdown
                }
                catch (TimeoutException)
                {
                    // Timeout is normal in RTU - just continue listening
                    continue;
                }
                catch (Exception ex)
                {
                    CommunicationError?.Invoke(this, $"Error processing frame: {ex.Message}");

                    // Small delay before continuing to prevent tight error loops
                    await Task.Delay(100, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            CommunicationError?.Invoke(this, $"Fatal server error: {ex.Message}");
        }
        finally
        {
            try
            {
                if (_port.IsOpen)
                {
                    _port.Close();
                }
            }
            catch (Exception ex)
            {
                CommunicationError?.Invoke(this, $"Error closing port: {ex.Message}");
            }
        }
    }

    private async Task<byte[]?> ReceiveFrame(byte[] buffer, CancellationToken cancellationToken)
    {
        var bytesRead = 0;
        var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_receiveTimeoutMs);

        try
        {
            // Wait for the first byte
            while (_port.BytesToRead == 0 && !timeoutCts.Token.IsCancellationRequested)
            {
                await Task.Delay(5, timeoutCts.Token);
            }

            if (timeoutCts.Token.IsCancellationRequested)
            {
                throw new TimeoutException("Timeout waiting for frame start");
            }

            var lastReceiveTime = DateTime.UtcNow;

            // Read bytes until inter-frame gap
            while (bytesRead < buffer.Length && !timeoutCts.Token.IsCancellationRequested)
            {
                if (_port.BytesToRead > 0)
                {
                    var available = Math.Min(_port.BytesToRead, buffer.Length - bytesRead);
                    var read = _port.Read(buffer, bytesRead, available);
                    bytesRead += read;
                    lastReceiveTime = DateTime.UtcNow;
                }
                else
                {
                    // Check for inter-frame gap
                    if ((DateTime.UtcNow - lastReceiveTime).TotalMilliseconds >= _interFrameDelayMs)
                    {
                        break; // End of frame
                    }

                    await Task.Delay(5, timeoutCts.Token);
                }
            }

            if (bytesRead < 4) // Minimum Modbus RTU frame size
            {
                return null;
            }

            var frame = new byte[bytesRead];
            Array.Copy(buffer, frame, bytesRead);
            return frame;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // This is a timeout, not a real cancellation
            throw new TimeoutException("Timeout during frame reception");
        }
        finally
        {
            timeoutCts.Dispose();
        }
    }

    private async Task ProcessFrame(byte[] frame, CancellationToken cancellationToken)
    {
        if (frame.Length < 4)
            return; // Invalid frame

        var modbusAddress = frame[0];
        var function = (ModbusFunction)frame[1];

        // Validate CRC
        var expectedCrc = RtuHelpers.Crc(frame, 0, frame.Length - 2);
        var actualCrc = (ushort)(frame[frame.Length - 2] | (frame[frame.Length - 1] << 8));

        if (expectedCrc != actualCrc)
        {
            CrcErrorDetected?.Invoke(this, EventArgs.Empty);
            return; // Per Modbus spec, ignore frames with CRC errors
        }

        IModbusResult? result = null;
        ushort? startRegister = null;
        ushort? writeValue = null;

        try
        {
            switch (function)
            {
                case ModbusFunction.ReadHoldingRegister:
                    result = ProcessReadHoldingRegister(frame, modbusAddress);
                    break;

                case ModbusFunction.ReadInputRegister:
                    result = ProcessReadInputRegister(frame, modbusAddress);
                    break;

                case ModbusFunction.ReadCoil:
                    result = ProcessReadCoil(frame, modbusAddress);
                    break;

                case ModbusFunction.ReadDiscrete:
                    result = ProcessReadDiscrete(frame, modbusAddress);
                    break;

                case ModbusFunction.WriteRegister:
                    result = ProcessWriteSingleRegister(frame, modbusAddress, out startRegister, out writeValue);
                    break;

                case ModbusFunction.WriteMultipleRegisters:
                    result = ProcessWriteMultipleRegisters(frame, modbusAddress, out startRegister, out writeValue);
                    break;

                case ModbusFunction.WriteCoil:
                    result = ProcessWriteSingleCoil(frame, modbusAddress, out startRegister, out writeValue);
                    break;

                case ModbusFunction.WriteMultipleCoils:
                    result = ProcessWriteMultipleCoils(frame, modbusAddress, out startRegister, out writeValue);
                    break;

                default:
                    result = new ModbusErrorResult(ModbusErrorCode.IllegalFunction);
                    break;
            }
        }
        catch (Exception ex)
        {
            CommunicationError?.Invoke(this, $"Error processing function {function}: {ex.Message}");
            result = new ModbusErrorResult(ModbusErrorCode.DeviceFailure);
        }

        // Send response
        if (result != null)
        {
            await SendResponse(function, modbusAddress, result, startRegister, writeValue, cancellationToken);
        }
    }

    private IModbusResult? ProcessReadHoldingRegister(byte[] frame, byte modbusAddress)
    {
        if (frame.Length != 8) return new ModbusErrorResult(ModbusErrorCode.IllegalDataValue);

        var startRegister = (ushort)((frame[2] << 8) | frame[3]);
        var length = (short)((frame[4] << 8) | frame[5]);

        return ReadHoldingRegisterRequest?.Invoke(modbusAddress, startRegister, length);
    }

    private IModbusResult? ProcessReadInputRegister(byte[] frame, byte modbusAddress)
    {
        if (frame.Length != 8) return new ModbusErrorResult(ModbusErrorCode.IllegalDataValue);

        var startRegister = (ushort)((frame[2] << 8) | frame[3]);
        var length = (short)((frame[4] << 8) | frame[5]);

        return ReadInputRegisterRequest?.Invoke(modbusAddress, startRegister, length);
    }

    private IModbusResult? ProcessReadCoil(byte[] frame, byte modbusAddress)
    {
        if (frame.Length != 8) return new ModbusErrorResult(ModbusErrorCode.IllegalDataValue);

        var startRegister = (ushort)((frame[2] << 8) | frame[3]);
        var length = (short)((frame[4] << 8) | frame[5]);

        return ReadCoilRequest?.Invoke(modbusAddress, startRegister, length);
    }

    private IModbusResult? ProcessReadDiscrete(byte[] frame, byte modbusAddress)
    {
        if (frame.Length != 8) return new ModbusErrorResult(ModbusErrorCode.IllegalDataValue);

        var startRegister = (ushort)((frame[2] << 8) | frame[3]);
        var length = (short)((frame[4] << 8) | frame[5]);

        return ReadDiscreteRequest?.Invoke(modbusAddress, startRegister, length);
    }

    private IModbusResult? ProcessWriteSingleRegister(byte[] frame, byte modbusAddress, out ushort? startRegister, out ushort? writeValue)
    {
        startRegister = null;
        writeValue = null;

        if (frame.Length != 8) return new ModbusErrorResult(ModbusErrorCode.IllegalDataValue);

        startRegister = (ushort)((frame[2] << 8) | frame[3]);
        writeValue = (ushort)((frame[4] << 8) | frame[5]);

        return WriteRegisterRequest?.Invoke(modbusAddress, startRegister.Value, new ushort[] { writeValue.Value });
    }

    private IModbusResult? ProcessWriteMultipleRegisters(byte[] frame, byte modbusAddress, out ushort? startRegister, out ushort? writeValue)
    {
        startRegister = null;
        writeValue = null;

        if (frame.Length < 9) return new ModbusErrorResult(ModbusErrorCode.IllegalDataValue);

        startRegister = (ushort)((frame[2] << 8) | frame[3]);
        var registerCount = (ushort)((frame[4] << 8) | frame[5]);
        var byteCount = frame[6];
        writeValue = registerCount;

        if (frame.Length != 9 + byteCount) return new ModbusErrorResult(ModbusErrorCode.IllegalDataValue);
        if (byteCount != registerCount * 2) return new ModbusErrorResult(ModbusErrorCode.IllegalDataValue);

        var registers = new ushort[registerCount];
        for (var i = 0; i < registerCount; i++)
        {
            var index = 7 + (i * 2);
            registers[i] = (ushort)((frame[index] << 8) | frame[index + 1]);
        }

        return WriteRegisterRequest?.Invoke(modbusAddress, startRegister.Value, registers);
    }

    private IModbusResult? ProcessWriteSingleCoil(byte[] frame, byte modbusAddress, out ushort? startRegister, out ushort? writeValue)
    {
        startRegister = null;
        writeValue = null;

        if (frame.Length != 8) return new ModbusErrorResult(ModbusErrorCode.IllegalDataValue);

        startRegister = (ushort)((frame[2] << 8) | frame[3]);
        var coilValue = (ushort)((frame[4] << 8) | frame[5]);
        writeValue = coilValue;

        var value = coilValue == 0xFF00; // 0xFF00 = ON, 0x0000 = OFF

        return WriteCoilRequest?.Invoke(modbusAddress, startRegister.Value, new bool[] { value });
    }

    private IModbusResult? ProcessWriteMultipleCoils(byte[] frame, byte modbusAddress, out ushort? startRegister, out ushort? writeValue)
    {
        startRegister = null;
        writeValue = null;

        if (frame.Length < 9) return new ModbusErrorResult(ModbusErrorCode.IllegalDataValue);

        startRegister = (ushort)((frame[2] << 8) | frame[3]);
        var coilCount = (ushort)((frame[4] << 8) | frame[5]);
        var byteCount = frame[6];
        writeValue = coilCount;

        if (frame.Length != 9 + byteCount) return new ModbusErrorResult(ModbusErrorCode.IllegalDataValue);

        var expectedByteCount = (coilCount + 7) / 8;
        if (byteCount != expectedByteCount) return new ModbusErrorResult(ModbusErrorCode.IllegalDataValue);

        var coils = new bool[coilCount];
        for (var i = 0; i < coilCount; i++)
        {
            var byteIndex = 7 + (i / 8);
            var bitIndex = i % 8;
            coils[i] = (frame[byteIndex] & (1 << bitIndex)) != 0;
        }

        return WriteCoilRequest?.Invoke(modbusAddress, startRegister.Value, coils);
    }

    private async Task SendResponse(ModbusFunction function, byte modbusAddress, IModbusResult result,
        ushort? startRegister, ushort? writeValue, CancellationToken cancellationToken)
    {
        try
        {
            RtuResponse? response = null;

            if (result is ModbusReadResult mrr)
            {
                response = RtuResponse.CreateReadResponse(function, modbusAddress, mrr);
            }
            else if (result is ModbusWriteResult mwr)
            {
                response = RtuResponse.CreateWriteResponse(function, modbusAddress, startRegister!.Value, writeValue!.Value, mwr);
            }
            else if (result is ModbusErrorResult mer)
            {
                response = RtuResponse.CreateErrorResponse(function, modbusAddress, mer);
            }

            if (response != null)
            {
                var data = response.Serialize();

                // Use a small delay before sending to ensure proper frame timing
                await Task.Delay(1, cancellationToken);
                _port.Write(data);
            }
        }
        catch (Exception ex)
        {
            CommunicationError?.Invoke(this, $"Error sending response: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    protected virtual void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            if (disposing)
            {
                Stop();
                _port?.Dispose();
                _cancellationTokenSource?.Dispose();
            }

            IsDisposed = true;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}