using Microsoft.Extensions.DependencyInjection;

namespace Motiv.Serialization.AspNetCore.Tests;

/// <summary>
/// <c>AddDecisionSource</c> registers the log's read side and a <see cref="DecisionReproducer"/>
/// over it, and says which registration is missing when the reproducer cannot be built.
/// </summary>
public class DecisionSourceDiTests
{
    private sealed record Customer(bool IsActive);

    private static SpecBase<Customer, string> IsActive { get; } =
        Spec.Build((Customer c) => c.IsActive).WhenTrue("active").WhenFalse("inactive").Create();

    private sealed class ActiveRule() : Rule<Customer, string>("active-rule", IsActive);

    private static ServiceProvider Build(Action<MotivRulesBuilder> enroll)
    {
        var services = new ServiceCollection();
        var registry = new SpecRegistry().Register("is-active", IsActive);
        enroll(services.AddMotivRules(registry, new MotivRulesOptions().AddModel<Customer>("customer")));
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Should_resolve_a_reproducer_over_the_registered_source()
    {
        // Arrange — the same in-memory sink is the log's sink and the source read back
        var sink = new InMemoryDecisionSink();
        await using var provider = Build(rules => rules
            .AddRule<ActiveRule>()
            .AddRuleStore()
            .AddPropositions()
            .AddDecisionLog(sink, log => log.Capture.StoreWhole<Customer>())
            .AddDecisionSource(sink));

        // Act & Assert
        provider.GetRequiredService<IDecisionSource>().ShouldBeSameAs(sink);
        provider.GetRequiredService<DecisionReproducer>().ShouldBeSameAs(provider.GetRequiredService<DecisionReproducer>());
    }

    [Fact]
    public void Should_refuse_a_null_source_and_a_null_factory()
    {
        var rules = new ServiceCollection().AddMotivRules(new SpecRegistry(), new MotivRulesOptions());

        Should.Throw<ArgumentNullException>(() => rules.AddDecisionSource((IDecisionSource)null!)).ParamName!.ShouldBe("source");
        Should.Throw<ArgumentNullException>(() => rules.AddDecisionSource((Func<IServiceProvider, IDecisionSource>)null!)).ParamName!.ShouldBe("sourceFactory");
    }

    [Fact]
    public async Task Should_name_the_missing_store_when_the_reproducer_cannot_be_built()
    {
        var sink = new InMemoryDecisionSink();
        await using var provider = Build(rules => rules
            .AddRule<ActiveRule>()
            .AddPropositions()
            .AddDecisionLog(sink, log => log.Capture.StoreWhole<Customer>())
            .AddDecisionSource(_ => sink));

        var refused = Should.Throw<InvalidOperationException>(() => provider.GetRequiredService<DecisionReproducer>());

        refused.Message.ShouldContain("AddRuleStore");
    }

    [Fact]
    public async Task Should_expose_the_resolvers_on_the_log()
    {
        await using var log = new DecisionLog(new InMemoryDecisionSink(), new DecisionLogOptions());
        log.Resolve.ShouldNotBeNull();
    }
}
