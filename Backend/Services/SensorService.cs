using SmartDash.Models;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using System.Runtime.InteropServices.WindowsRuntime;

namespace SmartDash.Services;

public class SensorService
{
    private string _mode = "simulated";
    private readonly Random _random = new();
    private readonly IConfiguration _config;

    // ============================================================================
    // BLUETOOTH INTEGRATION - ARANET4
    // ============================================================================
    
    private BluetoothLEDevice? _aranetDevice;
    private bool _isBluetoothConnected;
    private DateTime _lastBluetoothRead;
    private string? _connectedDeviceAddress;
    private string? _connectedDeviceName;
    
    // Aranet4 GATT UUIDs - Support BOTH old and new firmware
    // NEW UUID (firmware v1.2.0+)
    private static readonly Guid ARANET4_SERVICE_UUID_NEW = Guid.Parse("0000fce0-0000-1000-8000-00805f9b34fb");
    // OLD UUID (firmware before v1.2.0)
    private static readonly Guid ARANET4_SERVICE_UUID_OLD = Guid.Parse("f0cd1400-95da-4f4b-9ac8-aa55d312af0c");
    // Reading characteristic (same for both versions)
    private static readonly Guid ARANET4_READING_UUID = Guid.Parse("f0cd1503-95da-4f4b-9ac8-aa55d312af0c");
    
    public SensorService(IConfiguration config)
    {
        _config = config;
    }

    public void SetMode(string mode)
    {
        _mode = mode;
    }

    // ============================================================================
    // MAIN READING METHOD - USES BLUETOOTH IN LIVE MODE
    // ============================================================================
    
    public async Task<SensorReadings> GetReadingsAsync()
    {
        if (_mode == "live")
        {
            if (!_isBluetoothConnected)
            {
                throw new Exception("Not connected to Bluetooth device. Switch to simulated mode or connect device.");
            }
            
            // In live mode, ONLY return real data - no fallback
            return await ReadAranetDataAsync();
        }
        
        // Simulated mode only when explicitly set
        return new SensorReadings
        {
            Co2 = _random.Next(400, 1200),
            Temperature = Math.Round(18.0 + _random.NextDouble() * 6.0, 1),
            Humidity = _random.Next(30, 60),
            Pressure = Math.Round(990 + _random.NextDouble() * 30, 1),
            Timestamp = DateTime.UtcNow
        };
    }
    
    // Sync wrapper for backward compatibility
    public SensorReadings GetReadings()
    {
        return GetReadingsAsync().Result;
    }

    // ============================================================================
    // BLUETOOTH METHOD 1: SCAN FOR ARANET4 DEVICES (BLE WATCHER)
    // ============================================================================
    
    public async Task<List<BluetoothDeviceInfo>> ScanForAranetDevicesAsync()
    {
        var devices = new List<BluetoothDeviceInfo>();
        var foundAddresses = new HashSet<ulong>(); // Track unique devices
        
        try
        {
            Console.WriteLine("[BT] === ARANET4 BLE SCANNER ===");
            Console.WriteLine("[BT] TIP: Press button on Aranet4 to wake it!");
            Console.WriteLine("");
            
            // METHOD 1: BLE Advertisement Watcher (for UNPAIRED devices)
            Console.WriteLine("[BT] Method 1: Active BLE scan (for unpaired devices)...");
            
            var watcher = new BluetoothLEAdvertisementWatcher
            {
                ScanningMode = BluetoothLEScanningMode.Active
            };
            
            var tcs = new TaskCompletionSource<bool>();
            
            watcher.Received += (sender, args) =>
            {
                if (args.Advertisement.LocalName.Contains("Aranet", StringComparison.OrdinalIgnoreCase))
                {
                    if (foundAddresses.Add(args.BluetoothAddress)) // Only add if new
                    {
                        devices.Add(new BluetoothDeviceInfo
                        {
                            Name = args.Advertisement.LocalName,
                            Address = args.BluetoothAddress.ToString(),
                            RSSI = args.RawSignalStrengthInDBm,
                            IsConnected = false,
                            LastSeen = DateTime.UtcNow
                        });
                        Console.WriteLine($"[BT] ✓ Found BLE: {args.Advertisement.LocalName} (RSSI: {args.RawSignalStrengthInDBm}dBm)");
                    }
                }
            };
            
            watcher.Stopped += (sender, args) => tcs.TrySetResult(true);
            
            Console.WriteLine("[BT] Scanning for 5 seconds...");
            watcher.Start();
            await Task.Delay(5000); // Scan for 5 seconds
            watcher.Stop();
            await tcs.Task;
            
            Console.WriteLine($"[BT] BLE scan found {devices.Count} device(s)");
            Console.WriteLine("");
            
            // METHOD 2: Check paired/known devices (backup)
            if (devices.Count == 0)
            {
                Console.WriteLine("[BT] Method 2: Checking for paired devices...");
                var pairedDevices = await DeviceInformation.FindAllAsync(
                    BluetoothLEDevice.GetDeviceSelector());
                
                foreach (var deviceInfo in pairedDevices)
                {
                    if (deviceInfo.Name.Contains("Aranet", StringComparison.OrdinalIgnoreCase))
                    {
                        devices.Add(new BluetoothDeviceInfo
                        {
                            Name = deviceInfo.Name,
                            Address = deviceInfo.Id,
                            RSSI = 0,
                            IsConnected = false,
                            LastSeen = DateTime.UtcNow
                        });
                        Console.WriteLine($"[BT] ✓ Found PAIRED: {deviceInfo.Name}");
                    }
                }
            }
            
            Console.WriteLine("");
            Console.WriteLine($"[BT] === SCAN COMPLETE: Found {devices.Count} Aranet4(s) ===");
            
            if (devices.Count == 0)
            {
                Console.WriteLine("");
                Console.WriteLine("[BT] ⚠️  TROUBLESHOOTING:");
                Console.WriteLine("[BT] 1. Press the Aranet4 button to wake it");
                Console.WriteLine("[BT] 2. Make sure it's within 5 meters");
                Console.WriteLine("[BT] 3. Check Windows Bluetooth is ON");
                Console.WriteLine("[BT] 4. DON'T pair in Windows Settings (BLE doesn't need pairing!)");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Scan failed: {ex.Message}");
            Console.WriteLine($"[ERROR] Type: {ex.GetType().Name}");
        }
        
        return devices;
    }

    // ============================================================================
    // BLUETOOTH METHOD 2: CONNECT TO ARANET4 DEVICE
    // ============================================================================
    
    public async Task<bool> ConnectToAranetAsync(string deviceAddress = "")
    {
        try
        {
            Console.WriteLine($"[BT] Attempting to connect to Aranet4...");
            
            // If no address provided, scan and connect to first device
            if (string.IsNullOrEmpty(deviceAddress))
            {
                var devices = await ScanForAranetDevicesAsync();
                if (devices.Count == 0)
                {
                    Console.WriteLine("[ERROR] No Aranet4 devices found");
                    return false;
                }
                deviceAddress = devices[0].Address;
                Console.WriteLine($"[BT] Auto-connecting to: {devices[0].Name}");
            }
            
            // Try to parse address as ulong (from BLE watcher) or use as device ID
            if (ulong.TryParse(deviceAddress, out ulong bluetoothAddress))
            {
                // Direct BLE address connection (from watcher)
                Console.WriteLine($"[BT] Connecting via BLE address: {bluetoothAddress}");
                _aranetDevice = await BluetoothLEDevice.FromBluetoothAddressAsync(bluetoothAddress);
            }
            else
            {
                // Device ID connection (from paired devices)
                Console.WriteLine($"[BT] Connecting via device ID: {deviceAddress}");
                _aranetDevice = await BluetoothLEDevice.FromIdAsync(deviceAddress);
            }
            
            if (_aranetDevice == null)
            {
                Console.WriteLine("[ERROR] Failed to create device object");
                return false;
            }
            
            Console.WriteLine($"[BT] Device object created: {_aranetDevice.Name}");
            Console.WriteLine($"[BT] Connection status: {_aranetDevice.ConnectionStatus}");
            
            // IMPORTANT: Request pairing/bonding to unlock Aranet4 service
            Console.WriteLine("[BT] Checking pairing status...");
            var deviceInfo = await DeviceInformation.CreateFromIdAsync(_aranetDevice.DeviceId);
            
            if (deviceInfo.Pairing.IsPaired)
            {
                Console.WriteLine("[BT] ✓ Device is already paired");
            }
            else
            {
                Console.WriteLine("[BT] Device NOT paired - initiating pairing...");
                Console.WriteLine("[BT] NOTE: BLE devices auto-pair without user interaction");
                
                // Set up custom pairing for BLE (no user interaction needed)
                deviceInfo.Pairing.Custom.PairingRequested += (sender, args) =>
                {
                    Console.WriteLine($"[BT] Pairing requested: {args.PairingKind}");
                    
                    // For BLE devices, just accept the pairing automatically
                    if (args.PairingKind == DevicePairingKinds.ConfirmOnly)
                    {
                        args.Accept();
                        Console.WriteLine("[BT] Auto-accepted pairing (ConfirmOnly)");
                    }
                    else if (args.PairingKind == DevicePairingKinds.ProvidePin)
                    {
                        // Aranet4 shouldn't need a PIN, but just in case
                        args.Accept("000000"); // Default BLE PIN
                        Console.WriteLine("[BT] Auto-accepted with default PIN");
                    }
                    else
                    {
                        args.Accept();
                        Console.WriteLine($"[BT] Auto-accepted pairing: {args.PairingKind}");
                    }
                };
                
                // Use custom pairing to avoid UI popup
                var pairingResult = await deviceInfo.Pairing.Custom.PairAsync(
                    DevicePairingKinds.ConfirmOnly | DevicePairingKinds.ProvidePin,
                    DevicePairingProtectionLevel.None);
                
                if (pairingResult.Status == DevicePairingResultStatus.Paired)
                {
                    Console.WriteLine("[BT] ✓ Pairing successful!");
                }
                else if (pairingResult.Status == DevicePairingResultStatus.AlreadyPaired)
                {
                    Console.WriteLine("[BT] ✓ Device was already paired");
                }
                else
                {
                    Console.WriteLine($"[ERROR] Pairing failed: {pairingResult.Status}");
                    Console.WriteLine($"[ERROR] Protection level required: {pairingResult.ProtectionLevelUsed}");
                    
                    // Try to continue anyway - sometimes services are accessible without pairing
                    Console.WriteLine("[BT] Attempting to continue without pairing...");
                }
            }
            
            // Get GATT services (now that device is paired)
            Console.WriteLine("[BT] Getting GATT services...");
            var servicesResult = await _aranetDevice.GetGattServicesAsync(BluetoothCacheMode.Uncached);
            
            if (servicesResult.Status != GattCommunicationStatus.Success)
            {
                Console.WriteLine($"[ERROR] Failed to get GATT services: {servicesResult.Status}");
                Console.WriteLine($"[ERROR] Protocol error: {servicesResult.ProtocolError}");
                return false;
            }
            
            Console.WriteLine($"[BT] Found {servicesResult.Services.Count} GATT services");
            
            // Try NEW UUID first (v1.2.0+), then OLD UUID
            Console.WriteLine("[BT] Trying NEW service UUID (firmware v1.2.0+)...");
            var serviceResult = await _aranetDevice.GetGattServicesForUuidAsync(ARANET4_SERVICE_UUID_NEW);
            
            if (serviceResult.Status != GattCommunicationStatus.Success || serviceResult.Services.Count == 0)
            {
                Console.WriteLine("[BT] New UUID not found, trying OLD service UUID (pre-v1.2.0)...");
                serviceResult = await _aranetDevice.GetGattServicesForUuidAsync(ARANET4_SERVICE_UUID_OLD);
            }
            else
            {
                Console.WriteLine("[BT] ✓ Found service with NEW UUID (firmware v1.2.0+)");
            }
            
            if (serviceResult.Status != GattCommunicationStatus.Success || serviceResult.Services.Count == 0)
            {
                Console.WriteLine("[ERROR] Aranet4 service not found on device");
                Console.WriteLine($"[ERROR] Tried both NEW ({ARANET4_SERVICE_UUID_NEW}) and OLD ({ARANET4_SERVICE_UUID_OLD}) UUIDs");
                return false;
            }
            
            _isBluetoothConnected = true;
            _connectedDeviceAddress = deviceAddress;
            _connectedDeviceName = _aranetDevice.Name;
            
            Console.WriteLine($"[SUCCESS] Connected to {_aranetDevice.Name}!");
            Console.WriteLine($"[SUCCESS] Connection status: {_aranetDevice.ConnectionStatus}");
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Connection failed: {ex.Message}");
            Console.WriteLine($"[ERROR] Type: {ex.GetType().Name}");
            _isBluetoothConnected = false;
            return false;
        }
    }

    // ============================================================================
    // BLUETOOTH METHOD 3: READ SENSOR DATA FROM ARANET4
    // ============================================================================
    
    private async Task<SensorReadings> ReadAranetDataAsync()
    {
        if (_aranetDevice == null || !_isBluetoothConnected)
        {            throw new Exception("Not connected to Aranet4 device");
        }
        
        try
        {
            // Try NEW UUID first, then OLD UUID
            var serviceResult = await _aranetDevice.GetGattServicesForUuidAsync(ARANET4_SERVICE_UUID_NEW);
            if (serviceResult.Status != GattCommunicationStatus.Success || serviceResult.Services.Count == 0)
            {
                serviceResult = await _aranetDevice.GetGattServicesForUuidAsync(ARANET4_SERVICE_UUID_OLD);
            }
            
            if (serviceResult.Status != GattCommunicationStatus.Success || serviceResult.Services.Count == 0)
            {
                throw new Exception("Failed to get Aranet4 service");
            }
            
            var service = serviceResult.Services[0];
            
            // Get the reading characteristic
            var charResult = await service.GetCharacteristicsForUuidAsync(ARANET4_READING_UUID);
            if (charResult.Status != GattCommunicationStatus.Success || charResult.Characteristics.Count == 0)
            {
                throw new Exception("Failed to get reading characteristic");
            }
            
            var characteristic = charResult.Characteristics[0];
            
            // Read the value
            var readResult = await characteristic.ReadValueAsync(BluetoothCacheMode.Uncached);
            if (readResult.Status != GattCommunicationStatus.Success)
            {
                throw new Exception($"Failed to read value: {readResult.Status}");
            }            
            // Parse the 9-byte data
            byte[] data = readResult.Value.ToArray();
            
            if (data.Length < 9)
            {
                throw new Exception($"Invalid data length: {data.Length} (expected 9)");
            }
            
            // MARK: NETWORK BLUETOOTH - Add HTTP/TCP read endpoint here
            // Example: data = await ReadFromNetworkTransceiverAsync();
            
            var readings = ParseAranet4Data(data);
            _lastBluetoothRead = DateTime.UtcNow;
            
            Console.WriteLine($"[BT] Read: CO2={readings.Co2}ppm, Temp={readings.Temperature}°C, Hum={readings.Humidity}%, Press={readings.Pressure}hPa");
            
            return readings;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Read failed: {ex.Message}");
            throw;
        }
    }

    // ============================================================================
    // BLUETOOTH METHOD 4: PARSE RAW ARANET4 DATA
    // ============================================================================
        
    private SensorReadings ParseAranet4Data(byte[] data)
    {
        // Aranet4 data format (9 bytes):
        // Byte 0-1: CO2 (u16LE) - ppm
        // Byte 2-3: Temperature (u16LE) - value * 0.05 = °C  
        // Byte 4-5: Pressure (u16LE) - value * 0.1 = hPa
        // Byte 6:   Humidity (u8) - %
        // Byte 7:   Battery (u8) - %
        // Byte 8:   Status (u8) - color indicator
        
        ushort co2Raw = BitConverter.ToUInt16(data, 0);
        ushort tempRaw = BitConverter.ToUInt16(data, 2);
        ushort pressRaw = BitConverter.ToUInt16(data, 4);
        byte humidity = data[6];
        byte battery = data[7];
        byte status = data[8];
        
        return new SensorReadings
        {
            Co2 = co2Raw,
            Temperature = Math.Round(tempRaw * 0.05, 1),
            Humidity = humidity,
            Pressure = Math.Round(pressRaw * 0.1, 1),
            Timestamp = DateTime.UtcNow
        };
    }

    // ============================================================================
    // BLUETOOTH METHOD 5: DISCONNECT FROM ARANET4
    // ============================================================================    
    public async Task DisconnectAranetAsync()
    {
        try
        {
            if (_aranetDevice != null)
            {
                Console.WriteLine($"[BT] Disconnecting from {_connectedDeviceName}...");
                
                _aranetDevice.Dispose();
                _aranetDevice = null;
                _isBluetoothConnected = false;
                _connectedDeviceAddress = null;
                _connectedDeviceName = null;
                
                // MARK: NETWORK BLUETOOTH - Close network connection here
                // Example: await DisconnectFromNetworkTransceiverAsync();
                
                Console.WriteLine("[BT] Disconnected");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Disconnect failed: {ex.Message}");
        }
        
        await Task.CompletedTask;
    }

    // ============================================================================
    // BLUETOOTH METHOD 6: GET CONNECTION STATUS
    // ============================================================================
        
    public BluetoothStatusResponse GetBluetoothStatus()
    {
        return new BluetoothStatusResponse
        {
            IsConnected = _isBluetoothConnected,
            DeviceName = _connectedDeviceName,
            DeviceAddress = _connectedDeviceAddress,
            LastReadTime = _lastBluetoothRead == default ? null : _lastBluetoothRead,
            ConnectionType = "PC Bluetooth" // TODO: Change to "Network Transceiver" when using network BT
        };
    }
}