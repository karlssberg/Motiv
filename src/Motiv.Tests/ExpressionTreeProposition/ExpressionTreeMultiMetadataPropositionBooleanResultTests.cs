using System.Linq.Expressions;
using Motiv.ExpressionTreeProposition;
using Motiv.Shared;

namespace Motiv.Tests.ExpressionTreeProposition;

public class ExpressionTreeMultiMetadataPropositionBooleanResultTests
{
    private static readonly Expression<Func<int, bool>> Expression = n => n > 0;

    private static BooleanResultBase<string> Underlying(bool satisfied) =>
        Spec.Build((int n) => n > 0)
            .WhenTrue("n > 0")
            .WhenFalse("n <= 0")
            .Create()
            .Evaluate(satisfied ? 1 : -1);

    private static ExpressionTreeMultiMetadataPropositionBooleanResult<int, int, bool> Create(
        bool satisfied,
        Func<int, BooleanResultBase<string>, IEnumerable<int>> metadataResolver) =>
        new(satisfied,
            satisfied ? 1 : -1,
            Underlying(satisfied),
            metadataResolver,
            Expression,
            new SpecDescription("is positive"));

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Satisfied_ShouldMirrorConstructorArgument(bool satisfied)
    {
        var result = Create(satisfied, (_, _) => [1, 2]);

        result.Satisfied.ShouldBe(satisfied);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Underlying_ShouldBeEmpty(bool satisfied)
    {
        var result = Create(satisfied, (_, _) => [1, 2]);

        result.Underlying.ShouldBeEmpty();
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void UnderlyingWithValues_ShouldBeEmpty(bool satisfied)
    {
        var result = Create(satisfied, (_, _) => [1, 2]);

        result.UnderlyingWithValues.ShouldBeEmpty();
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Causes_ShouldBeEmpty(bool satisfied)
    {
        var result = Create(satisfied, (_, _) => [1, 2]);

        result.Causes.ShouldBeEmpty();
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void CausesWithValues_ShouldBeEmpty(bool satisfied)
    {
        var result = Create(satisfied, (_, _) => [1, 2]);

        result.CausesWithValues.ShouldBeEmpty();
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void MetadataTier_ShouldExposeResolvedMetadata(bool satisfied)
    {
        var result = Create(satisfied, (_, _) => [1, 2]);

        result.MetadataTier.Metadata.ShouldBe([1, 2]);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Explanation_ShouldCoOptUnderlyingAssertions(bool satisfied)
    {
        var underlying = Underlying(satisfied);
        var result = new ExpressionTreeMultiMetadataPropositionBooleanResult<int, int, bool>(
            satisfied,
            satisfied ? 1 : -1,
            underlying,
            (_, _) => [1, 2],
            Expression,
            new SpecDescription("is positive"));

        result.Explanation.Assertions.ShouldBe(underlying.Assertions);
    }
}
