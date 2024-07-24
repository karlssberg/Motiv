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
        .Build((IsOccupied | IsNight) & IsCold & !IsHighEnergyUsage)
        .WhenTrue<ISmartHomeAction>((_, result) => new ActivateHeatingAction(result.Assertions))
        .WhenFalse((_, result) => new DeactivateHeatingAction(result.Assertions))
        .Create("heating is needed");

    private SpecBase<SmartHomeContext, ISmartHomeAction> ActivateAirCon => Spec
        .Build(IsOccupied & IsHot & !IsHighEnergyUsage)
        .WhenTrue<ISmartHomeAction>((_, result) => new ActivateAirConAction(result.Assertions))
        .WhenFalse((_, result) => new DeactivateAirConAction(result.Assertions))
        .Create("AC is needed");

    private SpecBase<SmartHomeContext, ISmartHomeAction> TurnLightsOn => Spec
        .Build(IsOccupied | IsNight)
        .WhenTrue<ISmartHomeAction>((_, result) => new KeepLightsOnAction(result.Assertions))
        .WhenFalse((_, result) => new TurnOffLightsAction(result.Assertions))
        .Create("lights should be on");

    private SpecBase<SmartHomeContext, ISmartHomeAction> CloseWindows => Spec
        .Build((IsCold | IsHot | !IsOccupied) & AreWindowsOpen)
        .WhenTrue<ISmartHomeAction>((_, result) => new CloseWindowsAction(result.Assertions))
        .WhenFalse((_, result) => new KeepWindowsOpenAction(result.Assertions))
        .Create("windows should be closed");

    private readonly SpecBase<SmartHomeContext, string> IsOccupied = Spec
        .Build((SmartHomeContext home) => home.IsOccupied)
        .WhenTrue("the home is occupied")
        .WhenFalse("the home is unoccupied")
        .Create();

    private readonly SpecBase<SmartHomeContext, string> IsNight = Spec
        .Build((SmartHomeContext home) => home.IsNight)
        .WhenTrue("it is nighttime")
        .WhenFalse("it is daytime")
        .Create();

    private readonly SpecBase<SmartHomeContext, string> IsCold = Spec
        .Build((SmartHomeContext home) => home.Temperature < 18)
        .WhenTrue("the temperature is below 18°C")
        .WhenFalse("the temperature is 18°C or above")
        .Create();

    private readonly SpecBase<SmartHomeContext, string> IsHot = Spec
        .Build((SmartHomeContext home) => home.Temperature > 25)
        .WhenTrue("temperature is above 25°C")
        .WhenFalse("temperature is 25°C or below")
        .Create();

    private readonly SpecBase<SmartHomeContext, string> IsHighEnergyUsage = Spec
        .Build((SmartHomeContext home) => home.EnergyUsage > 5000)
        .WhenTrue("energy usage is high")
        .WhenFalse("energy usage is normal")
        .Create();

    private readonly SpecBase<SmartHomeContext, string> AreWindowsOpen = Spec
        .Build((SmartHomeContext home) => home.WindowsOpen)
        .WhenTrue("the windows are open")
        .WhenFalse("the windows are closed")
        .Create();
}
