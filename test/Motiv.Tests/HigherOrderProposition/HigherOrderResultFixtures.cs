using System.Linq.Expressions;
using Motiv.HigherOrderProposition;
using Motiv.Shared;

namespace Motiv.Tests.HigherOrderProposition;

/// <summary>
/// Shared construction helpers for the specialized higher-order result classes. The underlying
/// <see cref="BooleanResult{TModel,TMetadata}"/>, <see cref="PolicyResult{TModel,TMetadata}"/> and
/// <see cref="ModelResult{TModel}"/> instances are built from real proposition evaluations so their
/// explanations and metadata tiers are populated exactly as they would be in production.
/// </summary>
internal static class HigherOrderResultFixtures
{
    internal const string Statement = "all satisfied";

    internal static ISpecDescription Description { get; } = new SpecDescription(Statement);

    internal static Expression<Func<int, bool>> Expression { get; } = model => model > 0;

    private static readonly PolicyBase<bool, string> StringPolicy =
        Spec.Build((bool b) => b)
            .WhenTrue("underlying true")
            .WhenFalse("underlying false")
            .Create();

    private static readonly PolicyBase<bool, int> IntPolicy =
        Spec.Build((bool b) => b)
            .WhenTrue(1)
            .WhenFalse(0)
            .Create("int underlying");

    internal static BooleanResult<int, string> StringBoolResult(bool satisfied) =>
        new(satisfied ? 1 : 0, StringPolicy.Evaluate(satisfied));

    internal static BooleanResult<int, int> IntBoolResult(bool satisfied) =>
        new(satisfied ? 1 : 0, IntPolicy.Evaluate(satisfied));

    internal static PolicyResult<int, string> StringPolicyResult(bool satisfied) =>
        new(satisfied ? 1 : 0, StringPolicy.Evaluate(satisfied));

    internal static PolicyResult<int, int> IntPolicyResult(bool satisfied) =>
        new(satisfied ? 1 : 0, IntPolicy.Evaluate(satisfied));

    internal static ModelResult<int> Model(bool satisfied) => new(satisfied ? 1 : 0, satisfied);

    internal static string ExpectedReason(bool satisfied) => satisfied ? $"{Statement} == true" : $"{Statement} == false";
}
