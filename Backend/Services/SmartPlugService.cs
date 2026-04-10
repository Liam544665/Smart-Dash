using SmartDash.Models;

namespace SmartDash.Services;

public class SmartPlugService
{
    private PlugStatus _state = new() { On = false, Power = 0, Current = 0 };
    private readonly Random _random = new();

    // Locked to simulated mode for Honours project demo
    // Kasa HS110 integration available for future deployment
    public void SetMode(string mode)
    {
        // No-op: locked to simulated mode
        Console.WriteLine($"⚠️ Smart Plug mode change ignored - locked to simulated mode");
    }

    public PlugStatus Toggle()
    {
        _state.On = !_state.On;

        if (_state.On)
        {
            // Simulate appliance power draw (realistic fluctuations)
            _state.Power = _random.Next(50, 200);
            _state.Current = Math.Round(_state.Power / (double)_state.Voltage, 2);
            Console.WriteLine($"🔌 Smart Plug toggled ON - Power: {_state.Power}W");
        }
        else
        {
            _state.Power = 0;
            _state.Current = 0;
            Console.WriteLine("🔌 Smart Plug toggled OFF");
        }

        return _state;
    }

    public PlugStatus GetStatus()
    {
        // Simulated mode: Add realistic power fluctuation when on
        if (_state.On)
        {
            _state.Power += _random.Next(-5, 5);
            _state.Power = Math.Max(45, Math.Min(205, _state.Power));
            _state.Current = Math.Round(_state.Power / (double)_state.Voltage, 2);
        }
        
        return _state;
    }
}
