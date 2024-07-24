namespace Motiv.SmartHome.Actions;

public class ActivateAirConAction(IEnumerable<string> assertions) : ISmartHomeAction
{
    public IEnumerable<string> Assertions => assertions;
    public void Execute() => Console.WriteLine($"Air conditioning activated - {Assertions.Serialize()}");
}
