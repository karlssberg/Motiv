namespace Motiv.SmartHome.Actions;

public class KeepLightsOnAction(IEnumerable<string> assertions) : ISmartHomeAction
{
    public IEnumerable<string> Assertions => assertions;
    public void Execute() => Console.WriteLine($"Lights kept on - {Assertions.Serialize()}");
}
