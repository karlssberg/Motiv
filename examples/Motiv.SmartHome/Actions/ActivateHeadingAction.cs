namespace Motiv.SmartHome.Actions;

public class ActivateHeatingAction(IEnumerable<string> assertions) : ISmartHomeAction
{
    public IEnumerable<string> Assertions => assertions;
    public void Execute() => Console.WriteLine($"Heating activated - {Assertions.Serialize()}");
}
