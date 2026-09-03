using Motiv.Serialization;

namespace Motiv.Serialization.Tests.Propositions;

/// <summary>
/// How deep a composition a <em>catalogue</em> can build, as opposed to a single document. Spec 3E
/// left decorator nesting recursive on the argument that its depth "comes from how many propositions
/// an author wraps around each other, which is bounded by the catalogue rather than by a request
/// body". Ticket <see href="https://github.com/karlssberg/Motiv/issues/145">#145</see> asked whether
/// a catalogue with deep reference chains can approach that ceiling. It reaches it exactly.
/// </summary>
/// <remarks>
/// Every link here is an ordinary authored proposition referencing the one before it, and each
/// composes one operator level plus one decorator level — <c>RuleBinder.Decorate</c> wraps every node
/// carrying a <c>name</c>, and <c>RuleBinder.Bind</c> wraps a named document's root. That is the
/// alternating shape, whose measured ceiling is 1,047 links synchronously and 261 asynchronously on a
/// 1 MB thread; past it the process aborts with a stack overflow no <c>catch</c> can see.
/// <para>
/// Nothing in the stack counts the chain. <c>MaxDocumentDepth</c> bounds one document's JSON nesting
/// and each link nests two levels; <c>MaxNodeCount</c> bounds one document's nodes and each link has
/// three; <c>MaxCompositionDepth</c> bounds the composed depth of one document and stops at a
/// <c>spec</c> leaf, so each link scores 1 however deep the proposition it references is. The cases
/// below state that as behaviour rather than leaving it as an argument.
/// </para>
/// </remarks>
public class PropositionChainDepthTests
{
    private sealed record Model(int Value);

    private sealed class ChainRule() : Rule<Model, string>(
        "chain",
        Spec.Build((Model m) => m.Value % 2 == 0).WhenTrue("even").WhenFalse("odd").Create());

    private static SpecRegistry NewRegistry() =>
        new SpecRegistry().Register(
            "m.is-even",
            Spec.Build((Model m) => m.Value % 2 == 0).WhenTrue("even").WhenFalse("odd").Create());

    /// <summary>
    /// The cap named for composition depth is set as low as it goes, and a two-hundred-link chain is
    /// still accepted — because it never sees through a <c>spec</c> reference. Each link's own
    /// document composes exactly one level, which is all the cap ever measures, so 200 is a sample of
    /// the behaviour rather than the bound: nothing here is what stops the chain at any length.
    /// </summary>
    [Fact]
    public async Task Should_accept_a_two_hundred_link_reference_chain_under_the_lowest_composition_cap()
    {
        var propositions = new PropositionSet(
            NewRegistry(),
            new InMemoryPropositionStore(),
            new RuleSerializerOptions { MaxCompositionDepth = 1 })
            .AddModel<Model>("m");
        propositions.Load();

        await CreateChain(propositions, links: 200);

        // 201 authored links plus the compiled leaf they bottom out on, none of them quarantined.
        propositions.Propositions.Count.ShouldBe(202);
        propositions.Find("m.p200").ShouldNotBeNull().Quarantine.ShouldBeEmpty();
    }

    /// <summary>
    /// And the chain evaluates, so the depth is real rather than an artefact of how it was stored:
    /// two hundred links of recursion on whatever stack the caller happens to be on.
    /// </summary>
    [Fact]
    public async Task Should_evaluate_a_two_hundred_link_reference_chain()
    {
        var propositions = new PropositionSet(NewRegistry(), new InMemoryPropositionStore())
            .AddModel<Model>("m");
        propositions.Load();

        await CreateChain(propositions, links: 200);

        var rule = new ChainRule();
        var rules = new RuleSet(propositions).Add(rule);
        (await rules.UpdateAsync(
                "chain",
                """{ "rule": { "spec": "m.p200" } }""",
                1,
                new RuleChangeProvenance("test")))
            .Outcome.ShouldBe(RuleUpdateOutcome.Updated);

        rule.Evaluate(new Model(2)).Satisfied.ShouldBeTrue();
    }

    /// <summary>
    /// <c>p0</c> wraps the compiled leaf; every later link names itself and references the one before
    /// it, which is what an authored catalogue built up over time looks like.
    /// </summary>
    private static async Task CreateChain(PropositionSet propositions, int links)
    {
        (await propositions.CreateAsync("m.p0", "m", """{ "rule": { "spec": "m.is-even" } }""", null))
            .Outcome.ShouldBe(PropositionUpdateOutcome.Created);

        for (var link = 1; link <= links; link++)
        {
            var json =
                $$"""
                  { "name": "m.p{{link}}",
                    "rule": { "and": [ { "spec": "m.p{{link - 1}}" }, { "spec": "m.is-even" } ] } }
                  """;

            (await propositions.CreateAsync($"m.p{link}", "m", json, null))
                .Outcome.ShouldBe(PropositionUpdateOutcome.Created);
        }
    }
}
