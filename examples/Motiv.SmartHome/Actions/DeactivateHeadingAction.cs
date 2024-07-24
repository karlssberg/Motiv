namespace Motiv.SmartHome.Actions;

public class DeactivateHeatingAction(IEnumerable<string> assertions) : ISmartHomeAction
{
    public IEnumerable<string> Assertions => assertions;
    public void Execute() => Console.WriteLine($"Heating deactivated - {Assertions.Serialize()}");
}
