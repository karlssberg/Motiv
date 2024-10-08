using Motiv.SmartHome.Actions;

namespace Motiv.SmartHome;

public class ActivateSmartHomeRules()
{
    private IEnumerable<SpecBase<SmartHomeContext, ISmartHomeAction>> Specs =>
    [
        ActivateHeating,
        ActivateAirCon,
        TurnLightsOn,
        CloseWindows
    ];

    public IEnumerable<ISmartHomeAction> Execute(SmartHomeContext context) =>
        Specs.SelectMany(spec => spec.IsSatisfiedBy(context).Values);

    private SpecBase<SmartHomeContext, ISmartHomeAction> ActivateHeating => Spec
        .Build((_isOccupied | _isNight) & _isCold & !_isHighEnergyUsage)
        .WhenTrue<ISmartHomeAction>((_, result) => new ActivateHeatingAction(result.Assertions))
        .WhenFalse((_, result) => new DeactivateHeatingAction(result.Assertions))
        .Create("heating is needed");

    private SpecBase<SmartHomeContext, ISmartHomeAction> ActivateAirCon => Spec
        .Build(_isOccupied & _isHot & !_isHighEnergyUsage)
        .WhenTrue<ISmartHomeAction>((_, result) => new ActivateAirConAction(result.Assertions))
        .WhenFalse((_, result) => new DeactivateAirConAction(result.Assertions))
        .Create("AC is needed");

    private SpecBase<SmartHomeContext, ISmartHomeAction> TurnLightsOn => Spec
        .Build(_isOccupied | _isNight)
        .WhenTrue<ISmartHomeAction>((_, result) => new KeepLightsOnAction(result.Assertions))
        .WhenFalse((_, result) => new TurnOffLightsAction(result.Assertions))
        .Create("lights should be on");

    private SpecBase<SmartHomeContext, ISmartHomeAction> CloseWindows => Spec
        .Build((_isCold | _isHot | !_isOccupied) & _areWindowsOpen)
        .WhenTrue<ISmartHomeAction>((_, result) => new CloseWindowsAction(result.Assertions))
        .WhenFalse((_, result) => new KeepWindowsOpenAction(result.Assertions))
        .Create("windows should be closed");

    private readonly SpecBase<SmartHomeContext, string> _isOccupied = Spec
        .From((SmartHomeContext home) => home.IsOccupied)
        .WhenTrue("the home is occupied")
        .WhenFalse("the home is unoccupied")
        .Create();

    private readonly SpecBase<SmartHomeContext, string> _isNight = Spec
        .From((SmartHomeContext home) => home.IsNight)
        .WhenTrue("it is nighttime")
        .WhenFalse("it is daytime")
        .Create();

    private readonly SpecBase<SmartHomeContext, string> _isCold = Spec
        .From((SmartHomeContext home) => home.Temperature < 18)
        .WhenTrue("the temperature is below 18°C")
        .WhenFalse("the temperature is 18°C or above")
        .Create();

    private readonly SpecBase<SmartHomeContext, string> _isHot = Spec
        .From((SmartHomeContext home) => home.Temperature > 25)
        .WhenTrue("temperature is above 25°C")
        .WhenFalse("temperature is 25°C or below")
        .Create();

    private readonly SpecBase<SmartHomeContext, string> _isHighEnergyUsage = Spec
        .From((SmartHomeContext home) => home.EnergyUsage > 5000)
        .WhenTrue("energy usage is high")
        .WhenFalse("energy usage is normal")
        .Create();

    private readonly SpecBase<SmartHomeContext, string> _areWindowsOpen = Spec
        .From((SmartHomeContext home) => home.WindowsOpen)
        .WhenTrue("the windows are open")
        .WhenFalse("the windows are closed")
        .Create();
}
