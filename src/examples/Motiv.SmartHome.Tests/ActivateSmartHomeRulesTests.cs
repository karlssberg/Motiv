using Motiv.SmartHome.Actions;
using Shouldly;

namespace Motiv.SmartHome.Tests;

public class ActivateSmartHomeRulesTests
{
    private readonly ActivateSmartHomeRules _rules = new();

    [Fact]
    public void ShouldActivateHeating_WhenColdAndOccupied()
    {
        var context = new SmartHomeContext
        {
            Temperature = 15,
            IsOccupied = true,
            IsNight = false,
            EnergyUsage = 3000,
            WindowsOpen = false
        };

        var result = _rules.Execute(context);

        result.ShouldContain(item => item is ActivateHeatingAction);
    }

    [Fact]
    public void ShouldActivateAirCon_WhenHotAndOccupied()
    {
        var context = new SmartHomeContext
        {
            Temperature = 28,
            IsOccupied = true,
            IsNight = false,
            EnergyUsage = 3000,
            WindowsOpen = false
        };

        var result = _rules.Execute(context);

        result.ShouldContain(item => item is ActivateAirConAction);
    }

    [Fact]
    public void ShouldTurnOffLights_WhenUnoccupiedAndDaytime()
    {
        var context = new SmartHomeContext
        {
            Temperature = 22,
            IsOccupied = false,
            IsNight = false,
            EnergyUsage = 3000,
            WindowsOpen = false
        };

        var result = _rules.Execute(context);

        result.ShouldContain(item => item is TurnOffLightsAction);
    }

    [Fact]
    public void ShouldCloseWindows_WhenColdAndWindowsOpen()
    {
        var context = new SmartHomeContext
        {
            Temperature = 15,
            IsOccupied = true,
            IsNight = false,
            EnergyUsage = 3000,
            WindowsOpen = true
        };

        var result = _rules.Execute(context);

        result.ShouldContain(item => item is CloseWindowsAction);
    }

    [Fact]
    public void ShouldNotActivateHeating_WhenWarmAndOccupied()
    {
        var context = new SmartHomeContext
        {
            Temperature = 20,
            IsOccupied = true,
            IsNight = false,
            EnergyUsage = 3000,
            WindowsOpen = false
        };

        var result = _rules.Execute(context);

        result.ShouldContain(item => item is DeactivateHeatingAction);
    }

    [Fact]
    public void ShouldNotActivateAirCon_WhenCoolAndOccupied()
    {
        var context = new SmartHomeContext
        {
            Temperature = 23,
            IsOccupied = true,
            IsNight = false,
            EnergyUsage = 3000,
            WindowsOpen = false
        };

        var result = _rules.Execute(context);

        result.ShouldContain(item => item is DeactivateAirConAction);
    }

    [Fact]
    public void ShouldKeepLightsOn_WhenOccupiedAndNighttime()
    {
        var context = new SmartHomeContext
        {
            Temperature = 22,
            IsOccupied = true,
            IsNight = true,
            EnergyUsage = 3000,
            WindowsOpen = false
        };

        var result = _rules.Execute(context);

        result.ShouldContain(item => item is KeepLightsOnAction);
    }

    [Fact]
    public void ShouldKeepWindowsOpen_WhenWarmAndOccupied()
    {
        var context = new SmartHomeContext
        {
            Temperature = 22,
            IsOccupied = true,
            IsNight = true,
            EnergyUsage = 3000,
            WindowsOpen = true
        };

        var result = _rules.Execute(context);

        result.ShouldContain(item => item is KeepWindowsOpenAction);
    }
}
