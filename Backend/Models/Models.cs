namespace SmartDash.Models;

public class PlugStatus
{
    public bool On { get; set; }
    public int Power { get; set; }
    public int Voltage { get; set; } = 230;
    public double Current { get; set; }
}

public class SensorReadings
{
    public int Co2 { get; set; }
    public double Temperature { get; set; }
    public int Humidity { get; set; }
    public double Pressure { get; set; }
    public DateTime Timestamp { get; set; }
}

public class CameraSnapshot
{
    public string? StreamUrl { get; set; }
    public bool IsLive { get; set; }
    public DateTime Timestamp { get; set; }
}

public class SystemConfig
{
    public DeviceModeConfig Aranet { get; set; } = new();
    public DeviceModeConfig Plug { get; set; } = new();
    public CameraConfig Camera { get; set; } = new();
}

public class DeviceModeConfig
{
    public string Mode { get; set; } = "simulated"; // "simulated" or "live"
}

public class CameraConfig
{
    public string Mode { get; set; } = "simulated";
    public string StreamUrl { get; set; } = "rtsps://username:password@camera-ip:7447/camera-id";
}

public class ModeUpdateRequest
{
    public string Mode { get; set; } = "simulated";
}

public class DeviceModeUpdateRequest
{
    public string Device { get; set; } = ""; // "aranet", "plug", or "camera"
    public string Mode { get; set; } = "simulated"; // "simulated" or "live"
}

public class CameraUrlUpdateRequest
{
    public string Url { get; set; } = "";
}

// ============================================================================
// BLUETOOTH INTEGRATION - ADDED FOR ARANET4
// ============================================================================

public class BluetoothDeviceInfo
{
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public int RSSI { get; set; }
    public bool IsConnected { get; set; }
    public DateTime LastSeen { get; set; }
}

public class Aranet4RawData
{
    public ushort CO2Raw { get; set; }
    public ushort TemperatureRaw { get; set; }
    public ushort PressureRaw { get; set; }
    public byte Humidity { get; set; }
    public byte Battery { get; set; }
    public byte Status { get; set; }
}

public class BluetoothConnectionRequest
{
    public string DeviceAddress { get; set; } = "";
}

public class BluetoothStatusResponse
{
    public bool IsConnected { get; set; }
    public string? DeviceName { get; set; }
    public string? DeviceAddress { get; set; }
    public DateTime? LastReadTime { get; set; }
    public string ConnectionType { get; set; } = "PC Bluetooth"; // "PC Bluetooth" or "Network Transceiver"
}
