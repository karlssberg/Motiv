using Motiv.HigherOrderProposition;

namespace Motiv.Tests.HigherOrderProposition;

public class BooleanResultTests
{
    [Theory, AutoData]
    public void Constructor_WhenGivenValidParameters_InitializesProperties(
        string model,
        BooleanResultBase<string> underlyingResult)
    {
        var booleanResult = new BooleanResult<string, string>(model, underlyingResult);

        booleanResult.Model.Should().Be(model);
        booleanResult.Satisfied.Should().Be(underlyingResult.Satisfied);
        booleanResult.Description.Should().Be(underlyingResult.Description);
        booleanResult.Explanation.Should().Be(underlyingResult.Explanation);
        booleanResult.MetadataTier.Should().Be(underlyingResult.MetadataTier);
        booleanResult.Underlying.Should().ContainSingle().Which.Should().Be(underlyingResult);
        booleanResult.UnderlyingWithValues.Should().ContainSingle().Which.Should().Be(underlyingResult);
        booleanResult.Causes.Should().ContainSingle().Which.Should().Be(underlyingResult);
        booleanResult.CausesWithValues.Should().ContainSingle().Which.Should().Be(underlyingResult);
    }

    [Theory, AutoData]
    public void Constructor_WhenGivenNullModel_ThrowsArgumentNullException(
        BooleanResultBase<string> underlyingResult)
    {
        Action act = () => new BooleanResult<string, string>(null, underlyingResult);

        act.Should().Throw<ArgumentNullException>().WithParameterName("model");
    }

    [Theory, AutoData]
    public void Constructor_WhenGivenNullUnderlyingResult_ThrowsArgumentNullException(
        string model)
    {
        Action act = () => new BooleanResult<string, string>(model, null);

        act.Should().Throw<ArgumentNullException>().WithParameterName("underlyingResult");
    }
}
