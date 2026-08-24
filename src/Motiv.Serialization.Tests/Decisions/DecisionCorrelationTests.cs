namespace Motiv.Serialization.Tests.Decisions;

/// <summary>
/// The correlation id, which rides the pin that already means "one decision's world". Several rules
/// evaluated inside one snapshot share one id because they were one decision — which is the whole
/// reason the snapshot exists.
/// </summary>
public class DecisionCorrelationTests
{
    private sealed class Customer(bool isActive)
    {
        public bool IsActive { get; } = isActive;
    }

    private static SpecBase<Customer, string> IsActive { get; } =
        Spec.Build((Customer c) => c.IsActive).WhenTrue("active").WhenFalse("inactive").Create();

    private static RuleSet ARuleSet() =>
        new(new SpecRegistry().Register("customer.is-active", IsActive));

    [Fact]
    public void Should_have_no_ambient_decision_when_nothing_is_pinned()
    {
        // Assert — an evaluation outside a pin is still a decision; it just has no shared identity
        DecisionSnapshot.Current.ShouldBeNull();
    }

    [Fact]
    public void Should_publish_the_open_snapshot_as_the_ambient_decision()
    {
        // Arrange
        var rules = ARuleSet();

        // Act
        using var snapshot = rules.PinSnapshot("corr-1", caller: "alice");

        // Assert
        DecisionSnapshot.Current.ShouldBeSameAs(snapshot);
        snapshot.CorrelationId.ShouldBe("corr-1");
        snapshot.Caller!.ShouldBe("alice");
    }

    [Fact]
    public void Should_mint_a_correlation_id_when_the_caller_supplies_none()
    {
        // Arrange
        var rules = ARuleSet();

        // Act
        using var first = rules.PinSnapshot();
        var minted = first.CorrelationId;

        // Assert — a decision always has an identity, whether or not anyone named it
        minted.ShouldNotBeNullOrWhiteSpace();
        first.Caller.ShouldBeNull();
    }

    [Fact]
    public void Should_give_two_separate_pins_two_separate_decisions()
    {
        // Arrange
        var rules = ARuleSet();

        // Act
        string first, second;
        using (var pin = rules.PinSnapshot()) first = pin.CorrelationId;
        using (var pin = rules.PinSnapshot()) second = pin.CorrelationId;

        // Assert
        first.ShouldNotBe(second);
    }

    [Fact]
    public void Should_let_an_inner_pin_join_the_decision_already_open()
    {
        // Arrange
        var rules = ARuleSet();

        // Act
        using var outer = rules.PinSnapshot("corr-outer", caller: "alice");
        using (var inner = rules.PinSnapshot("corr-inner", caller: "mallory"))
        {
            // Assert — nesting is safe, and an inner pin does not start a second decision or
            // relabel the one in progress
            inner.CorrelationId.ShouldBe("corr-outer");
            inner.Caller!.ShouldBe("alice");
            DecisionSnapshot.Current.ShouldBeSameAs(outer);
        }

        // Assert — nor does disposing it end the outer decision
        DecisionSnapshot.Current.ShouldBeSameAs(outer);
    }

    [Fact]
    public void Should_clear_the_ambient_decision_when_the_outermost_pin_is_disposed()
    {
        // Arrange
        var rules = ARuleSet();

        // Act
        using (rules.PinSnapshot()) { }

        // Assert
        DecisionSnapshot.Current.ShouldBeNull();
    }

    [Fact]
    public async Task Should_carry_the_decision_across_an_await()
    {
        // Arrange
        var rules = ARuleSet();

        // Act
        using var snapshot = rules.PinSnapshot("corr-async");
        await Task.Yield();
        await Task.Delay(1);

        // Assert — the pin follows the async flow, exactly as the generation pin already does
        DecisionSnapshot.Current?.CorrelationId.ShouldBe("corr-async");
    }

    [Fact]
    public void Should_share_one_decision_between_a_proposition_set_and_its_rule_set()
    {
        // Arrange — the two halves of one host must not be able to disagree about which decision is
        // in progress
        var propositions = new PropositionSet(
            new SpecRegistry().Register("customer.is-active", IsActive), new InMemoryPropositionStore())
            .AddModel<Customer>("customer");
        propositions.Load();
        var rules = new RuleSet(propositions);

        // Act
        using var outer = propositions.PinSnapshot("corr-shared");
        using var inner = rules.PinSnapshot();

        // Assert
        inner.CorrelationId.ShouldBe("corr-shared");
    }
}
