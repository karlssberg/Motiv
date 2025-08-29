using Motiv.HigherOrderProposition;
using Shouldly;

namespace Motiv.Tests.HigherOrderProposition;

public class BooleanResultTests
{
    [Theory, AutoData]
    public void Constructor_WhenGivenValidParameters_InitializesProperties(
        string model,
        BooleanResultBase<string> underlyingResult)
    {
        var booleanResult = new BooleanResult<string, string>(model, underlyingResult);

        booleanResult.Model.ShouldBe(model);
        booleanResult.Satisfied.ShouldBe(underlyingResult.Satisfied);
        booleanResult.Description.ShouldBe(underlyingResult.Description);
        booleanResult.Explanation.ShouldBe(underlyingResult.Explanation);
        booleanResult.MetadataTier.ShouldBe(underlyingResult.MetadataTier);
        booleanResult.Underlying.ShouldBe([underlyingResult]);
        booleanResult.UnderlyingWithValues.ShouldBe([underlyingResult]);
        booleanResult.Causes.ShouldBe([underlyingResult]);
        booleanResult.CausesWithValues.ShouldBe([underlyingResult]);
    }

    [Theory, AutoData]
    public void Constructor_WhenGivenNullModel_ThrowsArgumentNullException(
        BooleanResultBase<string> underlyingResult)
    {
        var act = () => new BooleanResult<string, string>(null!, underlyingResult);

        act.ShouldThrow(typeof(ArgumentNullException));
    }

    [Theory, AutoData]
    public void Constructor_WhenGivenNullUnderlyingResult_ThrowsArgumentNullException(
        string model)
    {
        var act = () => new BooleanResult<string, string>(model, null!);

        act.ShouldThrow(typeof(ArgumentNullException));
    }
}
