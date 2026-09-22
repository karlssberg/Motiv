using Motiv.Serialization;

namespace Motiv.Serialization.Tests.Propositions;

public class BindingScopeExclusionTests
{
    private static BindingScope Scope() => new(new SpecRegistry());

    [Fact]
    public async Task Should_serialise_whole_operations_across_awaits()
    {
        // Arrange — the inner Monitor cannot do this: it is released at the first await
        var scope = Scope();
        var observed = new List<string>();

        async Task Operation(string id) =>
            await scope.LockedAsync(async () =>
            {
                observed.Add($"{id}-enter");
                await Task.Yield();
                await Task.Delay(20);
                observed.Add($"{id}-exit");
            }, default);

        // Act
        await Task.WhenAll(Operation("a"), Operation("b"));

        // Assert — neither operation may interleave with the other.
        // Joined to a string on purpose: Shouldly's ShouldBeOneOf compares with
        // EqualityComparer<T>.Default, which for List<string> is reference equality and can never
        // match a literal. Comparing the joined string also puts the real order in the failure message.
        string.Join(",", observed).ShouldBeOneOf(
            "a-enter,a-exit,b-enter,b-exit",
            "b-enter,b-exit,a-enter,a-exit");
    }

    [Fact]
    public async Task Should_cancel_a_waiter_rather_than_hang_behind_a_stuck_store()
    {
        // Arrange — this is why the write path is async: a hung store must be escapable
        var scope = Scope();
        var held = new TaskCompletionSource<bool>();
        var entered = new TaskCompletionSource<bool>();

        var holder = scope.LockedAsync(async () =>
        {
            entered.SetResult(true);
            await held.Task;
        }, default);

        await entered.Task;
        using var cancellation = new CancellationTokenSource();

        // Act
        var waiter = scope.LockedAsync(() => Task.CompletedTask, cancellation.Token);
        cancellation.Cancel();

        // Assert
        await Should.ThrowAsync<OperationCanceledException>(async () => await waiter);

        held.SetResult(true);
        await holder;
    }

    [Fact]
    public async Task Should_release_the_gate_when_the_operation_throws()
    {
        // Arrange
        var scope = Scope();

        // Act
        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await scope.LockedAsync<int>(() => throw new InvalidOperationException("boom"), default));

        // Assert — a failed publish must not wedge every later one
        var reentered = await scope.LockedAsync(() => Task.FromResult(42), default);
        reentered.ShouldBe(42);
    }

    [Fact]
    public async Task Should_leave_the_synchronous_gate_usable_alongside_it()
    {
        // Arrange — the two tiers coexist; the inner Monitor is for data-structure mutation
        var scope = Scope();

        // Act
        var inner = scope.Locked(() => 1);
        var outer = await scope.LockedAsync(() => Task.FromResult(2), default);

        // Assert
        inner.ShouldBe(1);
        outer.ShouldBe(2);
    }
}
