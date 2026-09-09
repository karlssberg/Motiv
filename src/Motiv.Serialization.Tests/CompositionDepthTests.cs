namespace Motiv.Serialization.Tests;

/// <summary>
/// The depth caps at each of the four binder entry points. Every one of them composes its own tree,
/// so a guard on one is no evidence about another — which is the only reason these cases are four
/// near-identical shapes rather than one.
/// </summary>
/// <remarks>
/// The depth counted here is a single document's. That the same caps see <em>across</em> a chain of
/// authored propositions — which is what
/// <see href="https://github.com/karlssberg/Motiv/issues/201">#201</see> was about — is held by
/// <c>PropositionChainDepthTests</c>, since only a <c>PropositionSet</c> can compose one.
/// </remarks>
public class CompositionDepthTests
{
    private sealed class Outcome
    {
        public string Code { get; init; } = "";
    }

    /// <summary>Four nested named nodes: four decorator levels, and rather more composed ones.</summary>
    private const string DeeplyDecorated =
        """
        { "name": "d1",
          "rule": { "name": "d2", "not": {
                      "name": "d3", "not": { "name": "d4", "spec": "a" } } } }
        """;

    private static RuleSerializer Serializer(RuleSerializerOptions options) =>
        new(new SpecRegistry()
                .Register("a", Spec.Build((int n) => n > 0).WhenTrue("t").WhenFalse("f").Create())
                .Register("m", Spec.Build((int n) => n > 0)
                    .WhenTrue(new Outcome { Code = "POS" })
                    .WhenFalse(new Outcome { Code = "NEG" })
                    .Create("m")),
            options);

    private static readonly RuleSerializerOptions ShallowDecoratorCap = new() { MaxDecoratorDepth = 2 };

    private static readonly RuleSerializerOptions ShallowCompositionCap = new() { MaxCompositionDepth = 2 };

    [Fact]
    public void Should_refuse_a_too_deep_document_on_the_synchronous_explanation_binder() =>
        Serializer(ShallowDecoratorCap).Validate<int>(DeeplyDecorated)
            .ShouldContain(error => error.Code == RuleErrorCode.DocumentTooLarge);

    [Fact]
    public void Should_refuse_a_too_deep_document_on_the_asynchronous_explanation_binder() =>
        Serializer(ShallowDecoratorCap).ValidateAsyncSpec<int>(DeeplyDecorated)
            .ShouldContain(error => error.Code == RuleErrorCode.DocumentTooLarge);

    [Fact]
    public void Should_refuse_a_too_deep_document_on_the_synchronous_metadata_binder() =>
        Serializer(ShallowDecoratorCap).Validate<int, Outcome>(DeeplyDecorated.Replace("\"a\"", "\"m\""))
            .ShouldContain(error => error.Code == RuleErrorCode.DocumentTooLarge);

    [Fact]
    public void Should_refuse_a_too_deep_document_on_the_asynchronous_metadata_binder() =>
        Serializer(ShallowDecoratorCap).ValidateAsyncSpec<int, Outcome>(DeeplyDecorated.Replace("\"a\"", "\"m\""))
            .ShouldContain(error => error.Code == RuleErrorCode.DocumentTooLarge);

    /// <summary>
    /// The composition cap and the decorator cap are separate branches with separate messages, so a
    /// document refused by the first says so rather than blaming decorator nesting it does not have.
    /// </summary>
    [Fact]
    public void Should_name_the_composition_cap_when_it_is_the_one_exceeded()
    {
        var errors = Serializer(ShallowCompositionCap)
            .Validate<int>("""{ "rule": { "and": [ { "spec": "a" }, { "spec": "a" }, { "spec": "a" }, { "spec": "a" } ] } }""");

        errors.ShouldContain(error =>
            error.Code == RuleErrorCode.DocumentTooLarge && error.Message.Contains("composition depth"));
    }

    /// <summary>And a document refused by the decorator cap names that one.</summary>
    [Fact]
    public void Should_name_the_decorator_cap_when_it_is_the_one_exceeded() =>
        Serializer(ShallowDecoratorCap).Validate<int>(DeeplyDecorated)
            .ShouldContain(error =>
                error.Code == RuleErrorCode.DocumentTooLarge && error.Message.Contains("decorator depth"));

    /// <summary>
    /// A document that stays within both caps binds, so the guards refuse depth rather than refusing
    /// everything — the failure mode a cap wired to the wrong side of a comparison would have.
    /// </summary>
    [Fact]
    public void Should_bind_a_document_within_both_caps() =>
        Serializer(new RuleSerializerOptions()).Validate<int>(DeeplyDecorated).ShouldBeEmpty();
}
