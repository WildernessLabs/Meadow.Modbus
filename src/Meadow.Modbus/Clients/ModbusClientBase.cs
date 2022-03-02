using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Meadow.Modbus
{
    public abstract class ModbusClientBase : IModbusBusClient, IDisposable
    {
        private const int MaxRegisterReadCount = 125;

        public event EventHandler Disconnected = delegate { };
        public event EventHandler Connected = delegate { };

        private bool _connected;
        private SemaphoreSlim _syncRoot = new SemaphoreSlim(1, 1);

        public bool IsDisposed { get; private set; }

        protected abstract byte[] GenerateWriteMessage(byte modbusAddress, ModbusFunction function, ushort register, byte[] data);
        protected abstract byte[] GenerateReadMessage(byte modbusAddress, ModbusFunction function, ushort startRegister, int registerCount);

        protected abstract Task DeliverMessage(byte[] message);
        protected abstract Task<byte[]> ReadResult(ModbusFunction function);

        public abstract Task Connect();
        public abstract void Disconnect();

        protected virtual void DisposeManagedResources() { }
        protected virtual void DisposeUnmanagedResources() { }

        public ModbusClientBase()
        {
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!IsDisposed)
            {
                if (disposing)
                {
                    DisposeManagedResources();
                }

                DisposeUnmanagedResources();

                IsDisposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public bool IsConnected
        {
            get => _connected;
            protected set
            {
                _connected = value;

                if (_connected)
                {
                    Connected?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    Disconnected?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public async Task WriteHoldingRegister(byte modbusAddress, ushort register, ushort value)
        {
            if (register > 40000)
            {
                // holding registers are defined as starting at 40001, but the actual bus read doesn't use the address, but instead the offset
                // we'll support th user passing in the definition either way
                register -= 40001;
            }

            // swap endianness, because Modbus
            var data = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)value));
            var message = GenerateWriteMessage(modbusAddress, ModbusFunction.WriteRegister, register, data);
            await _syncRoot.WaitAsync();

            try
            {
                await DeliverMessage(message);
                await ReadResult(ModbusFunction.WriteRegister);
            }
            finally
            {
                _syncRoot.Release();
            }
        }

        public async Task<ushort[]> ReadHoldingRegisters(byte modbusAddress, ushort startRegister, int registerCount)
        {
            if (startRegister > 40000)
            {
                // holding registers are defined as starting at 40001, but the actual bus read doesn't use the address, but instead the offset
                // we'll support th user passing in the definition either way
                startRegister -= 40001;
            }

            if (registerCount > MaxRegisterReadCount) throw new ArgumentException($"A maximum of {MaxRegisterReadCount} registers can be retrieved at one time");

            var message = GenerateReadMessage(modbusAddress, ModbusFunction.ReadHoldingRegister, startRegister, registerCount);
            await _syncRoot.WaitAsync();

            byte[] result;

            try
            {
                await DeliverMessage(message);
                result = await ReadResult(ModbusFunction.ReadHoldingRegister);
            }
            finally
            {
                _syncRoot.Release();
            }

            var registers = new ushort[registerCount];
            for (var i = 0; i < registerCount; i++)
            {
                registers[i] = (ushort)((result[i * 2] << 8) | (result[i * 2 + 1]));
            }
            return registers;
        }

        public async Task WriteCoil(byte modbusAddress, ushort register, bool value)
        {
            var data = value ? new byte[] { 0xff, 0xff } : new byte[] { 0x00, 0x00 };

            var message = GenerateWriteMessage(modbusAddress, ModbusFunction.WriteCoil, register, data);

            await _syncRoot.WaitAsync();
            try
            {
                await DeliverMessage(message);
                await ReadResult(ModbusFunction.WriteCoil);
            }
            finally
            {
                _syncRoot.Release();
            }
        }

        public async Task<bool[]> ReadCoils(byte modbusAddress, ushort startCoil, int coilCount)
        {
            if (coilCount > MaxRegisterReadCount) throw new ArgumentException($"A maximum of {MaxRegisterReadCount} coils can be retrieved at one time");

            var message = GenerateReadMessage(modbusAddress, ModbusFunction.ReadCoil, startCoil, coilCount);
            await _syncRoot.WaitAsync();

            byte[] result;

            try
            {
                await DeliverMessage(message);
                result = await ReadResult(ModbusFunction.ReadHoldingRegister);
            }
            finally
            {
                _syncRoot.Release();
            }

            int currentValue = 0;
            int currentBit;
            var values = new bool[coilCount];

            for (var i = 0; i < result.Length; i++)
            {
                currentBit = 0;
                while (currentValue < coilCount && currentBit < 8)
                {
                    var r = result[i] & (1 << currentBit++);
                    values[currentValue++] = r != 0;
                }
            }

            return values;
        }
    }
}
