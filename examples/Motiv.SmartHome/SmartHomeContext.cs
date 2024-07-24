public record SmartHomeContext
{
    public double Temperature { get; init; }
    public bool IsOccupied { get; init; }
    public bool IsNight { get; init; }
    public double EnergyUsage { get; init; }
    public bool WindowsOpen { get; init; }
}
