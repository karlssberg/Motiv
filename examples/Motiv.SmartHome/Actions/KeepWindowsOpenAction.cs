namespace Motiv.SmartHome.Actions;

public class KeepWindowsOpenAction(IEnumerable<string> assertions) : ISmartHomeAction
{
    public IEnumerable<string> Assertions => assertions;
    public void Execute() => Console.WriteLine($"Windows kept open - {Assertions.Serialize()}");
}
