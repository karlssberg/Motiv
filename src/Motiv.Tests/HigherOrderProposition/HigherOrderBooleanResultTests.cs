using AutoFixture;
using Motiv.HigherOrderProposition;

namespace Motiv.Tests.HigherOrderProposition;

public class HigherOrderBooleanResultTests
{
    [Theory, AutoData]
    public void Constructor_ShouldSetProperties_Correctly(IFixture fixture)
    {
        // Arrange
        var isSatisfied = fixture.Create<bool>();
        var metadata = fixture.CreateMany<TestMetadata>().ToList();
        var assertions = fixture.CreateMany<string>().ToList();
        var description = fixture.Create<ResultDescriptionBase>();
        var underlyingResults = fixture.CreateMany<BooleanResultBase<TestMetadata>>().ToList();
        var causes = fixture.CreateMany<BooleanResultBase<TestMetadata>>().ToList();

        // Act
        var result = new HigherOrderBooleanResult<TestMetadata, TestMetadata>(
                                      isSatisfied,
                                      new Lazy<IEnumerable<TestMetadata>>(() => metadata),
                                      new Lazy<IEnumerable<string>>(() => assertions),
                                      new Lazy<ResultDescriptionBase>(() => description),
                                      underlyingResults,
                                      new Lazy<IEnumerable<BooleanResultBase<TestMetadata>>>(() => causes));

        // Assert
        result.Satisfied.ShouldBe(isSatisfied);
        result.MetadataTier.Metadata.ShouldBe(metadata);
        result.Explanation.Assertions.ShouldBe(assertions);
        result.Description.ShouldBe(description);
        result.Underlying.ShouldBe(underlyingResults);
        result.Causes.ShouldBe(causes);
    }

    [Theory, AutoData]
    public void MetadataTier_ShouldBeLazyLoaded(IFixture fixture)
    {
        // Arrange
        var isSatisfied = fixture.Create<bool>();
        var metadata = fixture.CreateMany<TestMetadata>().ToList();
        var assertions = fixture.CreateMany<string>().ToList();
        var description = fixture.Create<ResultDescriptionBase>();
        var underlyingResults = fixture.CreateMany<BooleanResultBase<TestMetadata>>().ToList();
        var causes = fixture.CreateMany<BooleanResultBase<TestMetadata>>().ToList();
        var callCount = 0;


        var result = new HigherOrderBooleanResult<TestMetadata, TestMetadata>(
                                      isSatisfied,
                                      new Lazy<IEnumerable<TestMetadata>>(GetMetadata),
                                      new Lazy<IEnumerable<string>>(() => assertions),
                                      new Lazy<ResultDescriptionBase>(() => description),
                                      underlyingResults,
                                      new Lazy<IEnumerable<BooleanResultBase<TestMetadata>>>(() => causes));

        // Act & Assert
        callCount.ShouldBe(0);
        _ = result.MetadataTier;
        callCount.ShouldBe(1);
        _ = result.MetadataTier;
        callCount.ShouldBe(1);
        return;

        IEnumerable<TestMetadata> GetMetadata()
        {
            callCount++;
            return metadata;
        }
    }

    [Theory, AutoData]
    public void Explanation_ShouldBeLazyLoaded(IFixture fixture)
    {
        // Arrange
        var callCount = 0;

        var isSatisfied = fixture.Create<bool>();
        var metadata = fixture.CreateMany<TestMetadata>().ToList();
        var assertions = fixture.CreateMany<string>().ToList();
        var description = fixture.Create<ResultDescriptionBase>();
        var underlyingResults = fixture.CreateMany<BooleanResultBase<TestMetadata>>().ToList();
        var causes = fixture.CreateMany<BooleanResultBase<TestMetadata>>().ToList();

        // Act
        var result = new HigherOrderBooleanResult<TestMetadata, TestMetadata>(
            isSatisfied,
            new Lazy<IEnumerable<TestMetadata>>(() => metadata),
            new Lazy<IEnumerable<string>>(GetAssertions),
            new Lazy<ResultDescriptionBase>(() => description),
            underlyingResults,
            new Lazy<IEnumerable<BooleanResultBase<TestMetadata>>>(() => causes));

        // Act & Assert
        callCount.ShouldBe(0);
        _ = result.Explanation;
        callCount.ShouldBe(1);
        _ = result.Explanation;
        callCount.ShouldBe(1);
        return;

        IEnumerable<string> GetAssertions()
        {
            callCount++;
            return assertions;
        }
    }

    [Theory, AutoData]
    public void UnderlyingWithValues_ShouldReturnCorrectType(IFixture fixture)
    {
        // Arrange
        var underlying = fixture.CreateMany<BooleanResultBase<TestMetadata>>().ToList();
        var result = new HigherOrderBooleanResult<TestMetadata, TestMetadata>(
            fixture.Create<bool>(),
            new Lazy<IEnumerable<TestMetadata>>(fixture.CreateMany<TestMetadata>),
            new Lazy<IEnumerable<string>>(fixture.CreateMany<string>),
            new Lazy<ResultDescriptionBase>(fixture.Create<ResultDescriptionBase>),
            underlying,
            new Lazy<IEnumerable<BooleanResultBase<TestMetadata>>>(fixture.CreateMany<BooleanResultBase<TestMetadata>>)
        );

        // Act
        var underlyingWithValues = result.UnderlyingWithValues;

        // Assert
        underlyingWithValues.ShouldBe(underlying);
    }

    [Theory, AutoData]
    public void CausesWithValues_ShouldReturnCorrectType(IFixture fixture)
    {
        // Arrange
        var causes = fixture.CreateMany<BooleanResultBase<TestMetadata>>().ToList();
        var result = new HigherOrderBooleanResult<TestMetadata, TestMetadata>(
            fixture.Create<bool>(),
            new Lazy<IEnumerable<TestMetadata>>(fixture.CreateMany<TestMetadata>),
            new Lazy<IEnumerable<string>>(fixture.CreateMany<string>),
            new Lazy<ResultDescriptionBase>(fixture.Create<ResultDescriptionBase>),
            fixture.CreateMany<BooleanResultBase<TestMetadata>>(),
            new Lazy<IEnumerable<BooleanResultBase<TestMetadata>>>(() => causes)
        );

        // Act
        var causesWithValues = result.CausesWithValues;

        // Assert
        causesWithValues.ShouldBe(causes);
    }
}

public class TestMetadata;
