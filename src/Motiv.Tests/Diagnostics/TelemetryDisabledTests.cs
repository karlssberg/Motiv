using System.Diagnostics;

namespace Motiv.Tests.Diagnostics;

[Collection(TelemetryTestCollection.Name)]
public class TelemetryDisabledTests
{
    [Fact]
    public void Should_not_start_an_activity_when_nothing_is_listening()
    {
        var isEven = Spec.Build((int n) => n % 2 == 0).Create("is even");
        var isPositive = Spec.Build((int n) => n > 0).Create("is positive");

        (isEven & isPositive).Evaluate(2).Satisfied.ShouldBeTrue();

        Activity.Current.ShouldBeNull();
    }

    [Fact]
    public async Task Should_not_start_an_activity_asynchronously_when_nothing_is_listening()
    {
        var isEven = Spec.BuildAsync((int n) => Task.FromResult(n % 2 == 0)).Create("is even");

        (await isEven.EvaluateAsync(2)).Satisfied.ShouldBeTrue();

        Activity.Current.ShouldBeNull();
    }

    [Fact]
    public void Should_start_emitting_as_soon_as_a_listener_attaches_and_stop_when_it_detaches()
    {
        var isEven = Spec.Build((int n) => n % 2 == 0).Create("is even");

        using (var harness = new TelemetryHarness())
        {
            isEven.Evaluate(2);
            harness.Activities.Count.ShouldBe(1);
        }

        using var second = new TelemetryHarness();
        isEven.Evaluate(2);
        second.Activities.Count.ShouldBe(1);
    }
}
