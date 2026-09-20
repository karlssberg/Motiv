using Shouldly;
using Xunit;

namespace Motiv.Serialization.Tests.Decisions;

/// <summary>
/// The resolver seam: how a reference-only capture gets its model back for a reproduction. Sits in
/// the telemetry collection because it constructs <see cref="DecisionLogOptions"/> beside the log.
/// </summary>
[Collection(Diagnostics.RulesTelemetryTestCollection.Name)]
public class DecisionModelResolverTests
{
    private sealed record Customer(string Id);

    [Fact]
    public async Task Should_resolve_a_reference_through_the_registered_resolver()
    {
        var resolvers = new DecisionModelResolvers()
            .Reference<Customer>((key, _) => Task.FromResult<Customer?>(new Customer(key)));

        (await resolvers.ResolveAsync(typeof(Customer), "cust-42", default)).ShouldBe(new Customer("cust-42"));
        resolvers.Covers(typeof(Customer)).ShouldBeTrue();
    }

    [Fact]
    public void Should_refuse_a_null_resolver()
    {
        Should.Throw<ArgumentNullException>(() => new DecisionModelResolvers().Reference<Customer>(null!))
            .ParamName!.ShouldBe("resolver");
    }

    [Fact]
    public async Task Should_answer_null_when_nothing_is_registered_or_the_subject_is_gone()
    {
        var empty = new DecisionModelResolvers();
        (await empty.ResolveAsync(typeof(Customer), "cust-42", default)).ShouldBeNull();
        empty.Covers(typeof(Customer)).ShouldBeFalse();

        var erased = new DecisionModelResolvers().Reference<Customer>((_, _) => Task.FromResult<Customer?>(null));
        (await erased.ResolveAsync(typeof(Customer), "cust-42", default)).ShouldBeNull();
    }

    [Fact]
    public void Should_sit_beside_capture_on_the_log_options()
    {
        var options = new DecisionLogOptions();
        options.Resolve.Reference<Customer>((key, _) => Task.FromResult<Customer?>(new Customer(key)));
        options.Resolve.Covers(typeof(Customer)).ShouldBeTrue();
    }
}
