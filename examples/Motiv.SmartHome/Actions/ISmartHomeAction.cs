namespace Motiv.SmartHome.Actions;

public interface ISmartHomeAction
{
    public IEnumerable<string> Assertions { get; }
    public void Execute();
}
