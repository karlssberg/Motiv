namespace Motiv.SmartHome.Actions;

public class CloseWindowsAction(IEnumerable<string> assertions) : ISmartHomeAction
{
    public IEnumerable<string> Assertions => assertions;
    public void Execute() => Console.WriteLine($"Windows closed - {Assertions.Serialize()}");
}
