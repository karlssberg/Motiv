using Motiv.SmartHome;

var rulesEngine = new ActivateSmartHomeRules();

var home = new SmartHomeContext
{
    Temperature = 16,
    IsOccupied = true,
    IsNight = false,
    EnergyUsage = 4000,
    WindowsOpen = true
};

var actions = rulesEngine.Execute(home);

foreach (var action in actions)
{
    action.Execute();
}
