namespace Motiv.SmartHome.Actions;

public class DeactivateAirConAction(IEnumerable<string> assertions) : ISmartHomeAction
{
    public IEnumerable<string> Assertions => assertions;
    public void Execute() => Console.WriteLine($"Air conditioning deactivated - {Assertions.Serialize()}");
}
