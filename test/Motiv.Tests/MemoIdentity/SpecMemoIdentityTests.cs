namespace Motiv.Tests.MemoIdentity;

/// <summary>
/// The spec-level memos whose identity is externally visible: an operator spec's description, which the
/// telemetry path reads once per evaluation, and the explanation spec a metadata spec is adapted to.
/// </summary>
public class SpecMemoIdentityTests
{
    private static PolicyBase<bool, string> Policy(string name) => Spec.Build((bool b) => b).Create(name);

    private static SpecBase<bool, string> Proposition(string name) => Policy(name);

    private static AsyncPolicyBase<bool, string> AsyncPolicy(string name) =>
        Spec.BuildAsync((bool b) => new ValueTask<bool>(b)).Create(name);

    private static AsyncSpecBase<bool, string> AsyncProposition(string name) => AsyncPolicy(name);

    private static ExpressionSpecBase<int, string> ExpressionProposition(string name) =>
        Spec.From((int n) => n > 0).Create(name);

    private static ExpressionPolicyBase<int, string> ExpressionPolicy(string name) =>
        Spec.From((int n) => n > 0).WhenTrue(name).WhenFalse($"not {name}").Create();

    private static readonly (string Name, string SpecType, Func<SpecBase> Create)[] OperatorSpecs =
    [
        ("and spec", "AndSpec", () => Proposition("left").And(Proposition("right"))),
        ("or spec", "OrSpec", () => Proposition("left").Or(Proposition("right"))),
        ("xor spec", "XOrSpec", () => Proposition("left").XOr(Proposition("right"))),
        ("and-also spec", "AndAlsoSpec", () => Proposition("left").AndAlso(Proposition("right"))),
        ("or-else spec", "OrElseSpec", () => Proposition("left").OrElse(Proposition("right"))),
        ("not spec", "NotSpec", () => Proposition("operand").Not()),
        ("not policy", "NotPolicy", () => Policy("operand").Not()),
        ("and-also policy", "AndAlsoPolicy", () => Policy("left").AndAlso(Policy("right"))),
        ("or-else policy", "OrElsePolicy", () => Policy("left").OrElse(Policy("right"))),

        ("async and spec", "AsyncAndSpec", () => AsyncProposition("left").And(AsyncProposition("right"))),
        ("async or spec", "AsyncOrSpec", () => AsyncProposition("left").Or(AsyncProposition("right"))),
        ("async xor spec", "AsyncXOrSpec", () => AsyncProposition("left").XOr(AsyncProposition("right"))),
        ("async and-also spec", "AsyncAndAlsoSpec", () => AsyncProposition("left").AndAlso(AsyncProposition("right"))),
        ("async or-else spec", "AsyncOrElseSpec", () => AsyncProposition("left").OrElse(AsyncProposition("right"))),
        ("async not spec", "AsyncNotSpec", () => AsyncProposition("operand").Not()),
        ("async not policy", "AsyncNotPolicy", () => AsyncPolicy("operand").Not()),
        ("async and-also policy", "AsyncAndAlsoPolicy", () => AsyncPolicy("left").AndAlso(AsyncPolicy("right"))),
        ("async or-else policy", "AsyncOrElsePolicy", () => AsyncPolicy("left").OrElse(AsyncPolicy("right"))),

        ("expression and spec", "ExpressionAndSpec",
            () => ExpressionProposition("left").And(ExpressionProposition("right"))),
        ("expression or spec", "ExpressionOrSpec",
            () => ExpressionProposition("left").Or(ExpressionProposition("right"))),
        ("expression xor spec", "ExpressionXOrSpec",
            () => ExpressionProposition("left").XOr(ExpressionProposition("right"))),
        ("expression and-also spec", "ExpressionAndAlsoSpec",
            () => ExpressionProposition("left").AndAlso(ExpressionProposition("right"))),
        ("expression or-else spec", "ExpressionOrElseSpec",
            () => ExpressionProposition("left").OrElse(ExpressionProposition("right"))),
        ("expression not spec", "ExpressionNotSpec", () => ExpressionProposition("operand").Not()),
        ("expression not policy", "ExpressionNotPolicy", () => ExpressionPolicy("operand").Not()),
        ("expression and-also policy", "ExpressionAndAlsoPolicy",
            () => ExpressionPolicy("left").AndAlso(ExpressionPolicy("right"))),
        ("expression or-else policy", "ExpressionOrElsePolicy",
            () => ExpressionPolicy("left").OrElse(ExpressionPolicy("right"))),
    ];

    public static TheoryData<string> OperatorSpecNames
    {
        get
        {
            var names = new TheoryData<string>();
            foreach (var operatorSpec in OperatorSpecs)
                names.Add(operatorSpec.Name);

            return names;
        }
    }

    [Theory]
    [MemberData(nameof(OperatorSpecNames))]
    public void Operator_spec_description_is_the_same_instance_on_every_read(string name)
    {
        var (_, specType, create) = OperatorSpecs.Single(operatorSpec => operatorSpec.Name == name);

        var spec = create();

        TypeNameOf(spec).ShouldBe(specType, $"'{name}' no longer builds {specType}; re-point it so that class stays covered");
        spec.Description.ShouldBeSameAs(spec.Description);
    }

    [Fact]
    public void A_metadata_spec_adapts_to_the_same_explanation_spec_on_every_call()
    {
        var spec = Spec.Build((bool b) => b).WhenTrue(1).WhenFalse(0).Create("is true");

        spec.ToExplanationSpec().ShouldBeSameAs(spec.ToExplanationSpec());
    }

    [Fact]
    public void A_metadata_expression_policy_adapts_to_the_same_explanation_spec_on_every_call()
    {
        var policy = Spec.From((int n) => n > 0).WhenTrue(1).WhenFalse(0).Create("is positive");

        policy.ToExplanationSpec().ShouldBeSameAs(policy.ToExplanationSpec());
    }

    [Fact]
    public void A_metadata_expression_spec_adapts_to_the_same_explanation_spec_on_every_call()
    {
        var spec = Spec.From((int n) => n > 0)
            .WhenTrueYield((_, _) => new[] { 1 })
            .WhenFalseYield((_, _) => new[] { 0 })
            .Create("is positive");

        spec.ToExplanationSpec().ShouldBeSameAs(spec.ToExplanationSpec());
    }

    private static string TypeNameOf(object instance)
    {
        var name = instance.GetType().Name;
        var arity = name.IndexOf('`');

        return arity < 0 ? name : name.Substring(0, arity);
    }
}
