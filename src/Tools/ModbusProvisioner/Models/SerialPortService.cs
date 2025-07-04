using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;

namespace ModbusProvisioner;

public enum Device
{
    K114,
    Y4000
}

public class DeviceService
{
    public Device SelectedDevice { get; } = Device.K114;
}

public class SerialPortService
{
    public IEnumerable<string> GetPortNames()
    {
        return SerialPort.GetPortNames().Distinct();
    }
}
