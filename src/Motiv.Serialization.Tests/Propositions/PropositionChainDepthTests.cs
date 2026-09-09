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
/// <c>MaxDocumentDepth</c> bounds one document's JSON nesting and each link nests two levels;
/// <c>MaxNodeCount</c> bounds one document's nodes and each link has three. Neither sees a chain.
/// Since <see href="https://github.com/karlssberg/Motiv/issues/201">#201</see> the two that do are
/// <c>MaxCompositionDepth</c> and <c>MaxDecoratorDepth</c>, both measured at bind time against the
/// source a <c>spec</c> leaf resolves through, so a link scores the depth of what it references
/// rather than 1. The cases below hold that.
/// </para>
/// </remarks>
[Collection(MotivLimitsTestCollection.Name)]
public class PropositionChainDepthTests : IDisposable
{
    private readonly int _previousEvaluationSize = MotivLimits.MaxEvaluationSize;

    public void Dispose() => MotivLimits.MaxEvaluationSize = _previousEvaluationSize;

    private sealed record Model(int Value);

    private sealed class ChainRule() : Rule<Model, string>(
        "chain",
        Spec.Build((Model m) => m.Value % 2 == 0).WhenTrue("even").WhenFalse("odd").Create());

    private static SpecRegistry NewRegistry() =>
        new SpecRegistry().Register(
            "m.is-even",
            Spec.Build((Model m) => m.Value % 2 == 0).WhenTrue("even").WhenFalse("odd").Create());

    /// <summary>
    /// The cap named for composition depth now measures what it is named for. Each link composes an
    /// operator level and a decorator level over the link before it, so a chain two hundred long
    /// composes four hundred deep — and under a cap of one, the second link is already too deep to
    /// publish.
    /// </summary>
    [Fact]
    public async Task Should_refuse_a_reference_chain_that_composes_past_the_composition_cap()
    {
        var propositions = new PropositionSet(
            NewRegistry(),
            new InMemoryPropositionStore(),
            new RuleSerializerOptions { MaxCompositionDepth = 1 })
            .AddModel<Model>("m");
        propositions.Load();

        (await propositions.CreateAsync("m.p0", "m", """{ "rule": { "spec": "m.is-even" } }""", null))
            .Outcome.ShouldBe(PropositionUpdateOutcome.Created);

        var result = await propositions.CreateAsync(
            "m.p1",
            "m",
            """{ "name": "m.p1", "rule": { "and": [ { "spec": "m.p0" }, { "spec": "m.is-even" } ] } }""",
            null);

        result.Outcome.ShouldBe(PropositionUpdateOutcome.Invalid);
        result.Errors.ShouldContain(error => error.Code == RuleErrorCode.DocumentTooLarge);
    }

    /// <summary>
    /// The cap that bounds the stack rather than the cost. A chain grows one decorator level per
    /// link — <c>RuleBinder.Bind</c> wraps a named document's root — and it is decorator nesting,
    /// not the operator fold, that is still recursive after Spec 3E. Ten links, then the eleventh
    /// is refused.
    /// </summary>
    [Fact]
    public async Task Should_refuse_a_reference_chain_that_nests_past_the_decorator_cap()
    {
        var propositions = new PropositionSet(
            NewRegistry(),
            new InMemoryPropositionStore(),
            new RuleSerializerOptions { MaxDecoratorDepth = 10 })
            .AddModel<Model>("m");
        propositions.Load();

        await CreateChain(propositions, links: 10);

        var result = await propositions.CreateAsync(
            "m.p11",
            "m",
            """{ "name": "m.p11", "rule": { "and": [ { "spec": "m.p10" }, { "spec": "m.is-even" } ] } }""",
            null);

        result.Outcome.ShouldBe(PropositionUpdateOutcome.Invalid);
        result.Errors.ShouldContain(error => error.Code == RuleErrorCode.DocumentTooLarge);
    }

    /// <summary>
    /// A chain still binds and evaluates under the shipped defaults — the caps refuse the shapes that
    /// approach the measured ceiling, not ordinary catalogues.
    /// </summary>
    [Fact]
    public async Task Should_evaluate_a_reference_chain_within_the_default_caps()
    {
        var propositions = new PropositionSet(NewRegistry(), new InMemoryPropositionStore())
            .AddModel<Model>("m");
        propositions.Load();

        await CreateChain(propositions, links: 100);

        var rule = new ChainRule();
        var rules = new RuleSet(propositions).Add(rule);
        (await rules.UpdateAsync(
                "chain",
                """{ "rule": { "spec": "m.p100" } }""",
                1,
                new RuleChangeProvenance("test")))
            .Outcome.ShouldBe(RuleUpdateOutcome.Updated);

        rule.Evaluate(new Model(2)).Satisfied.ShouldBeTrue();
    }

    /// <summary>
    /// The one cap that does see the chain, since
    /// <see href="https://github.com/karlssberg/Motiv/issues/202">#202</see>. Each link composes an
    /// operator level and a decorator level, and the decorator re-enters the evaluation fold — which
    /// used to start a fresh count, so the chain evaluated under any limit at all however long it grew.
    /// <para>
    /// This is the same property <c>DecoratorSeamTests</c> holds against a hand-built alternating shape,
    /// stated here against the shape a stored catalogue actually composes. The hand-built one is a model
    /// of this; only this is the thing itself, and the whole reason the defect mattered was that a
    /// document reaches it.
    /// </para>
    /// <para>
    /// It bounds <em>size</em> and not <em>depth</em>; depth is
    /// <see href="https://github.com/karlssberg/Motiv/issues/201">#201</see>'s two caps above.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Should_refuse_a_reference_chain_that_exceeds_the_evaluation_size_bound()
    {
        var propositions = new PropositionSet(NewRegistry(), new InMemoryPropositionStore())
            .AddModel<Model>("m");
        propositions.Load();

        await CreateChain(propositions, links: 100);

        var rule = new ChainRule();
        var rules = new RuleSet(propositions).Add(rule);
        (await rules.UpdateAsync(
                "chain",
                """{ "rule": { "spec": "m.p100" } }""",
                1,
                new RuleChangeProvenance("test")))
            .Outcome.ShouldBe(RuleUpdateOutcome.Updated);

        // Bound only the evaluation: everything above is authoring, which the three document caps bound
        // and this one does not.
        MotivLimits.MaxEvaluationSize = 100;

        var act = () => rule.Evaluate(new Model(2));

        act.ShouldThrow<SpecException>();
    }

    /// <summary>
    /// The shipped defaults, with nothing configured: a chain is refused at the 129th link, because
    /// each link adds one decorator level and <c>MaxDecoratorDepth</c> defaults to 128. Before #201
    /// the chain grew without limit until the process aborted at 261 links asynchronously.
    /// </summary>
    [Fact]
    public async Task Should_refuse_a_reference_chain_past_the_default_decorator_cap()
    {
        var propositions = new PropositionSet(NewRegistry(), new InMemoryPropositionStore())
            .AddModel<Model>("m");
        propositions.Load();

        await CreateChain(propositions, links: 128);

        var result = await propositions.CreateAsync(
            "m.p129",
            "m",
            """{ "name": "m.p129", "rule": { "and": [ { "spec": "m.p128" }, { "spec": "m.is-even" } ] } }""",
            null);

        result.Outcome.ShouldBe(PropositionUpdateOutcome.Invalid);
        result.Errors.ShouldContain(error => error.Code == RuleErrorCode.DocumentTooLarge);
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
