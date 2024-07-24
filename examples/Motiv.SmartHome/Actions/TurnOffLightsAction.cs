namespace Motiv.SmartHome.Actions;

public class TurnOffLightsAction(IEnumerable<string> assertions) : ISmartHomeAction
{
    public IEnumerable<string> Assertions => assertions;
    public void Execute() => Console.WriteLine($"Lights turned off - {Assertions.Serialize()}");
}
