using Motiv.HigherOrderProposition;
using Motiv.Shared;

namespace Motiv.Tests.HigherOrderProposition;

    public class HigherOrderPolicyResultTests
    {
        public class TestMetadata { }
        public class TestUnderlyingMetadata { }

        private static HigherOrderPolicyResult<TestMetadata, TestUnderlyingMetadata> CreateHigherOrderPolicyResult(
            bool isSatisfied = true,
            TestMetadata? value = null,
            IEnumerable<TestMetadata>? metadata = null,
            IEnumerable<string>? assertions = null,
            string? reason = null,
            IEnumerable<BooleanResultBase<TestUnderlyingMetadata>>? underlyingResults = null,
            IEnumerable<BooleanResultBase<TestUnderlyingMetadata>>? causes = null)
        {
            return new HigherOrderPolicyResult<TestMetadata, TestUnderlyingMetadata>(
                isSatisfied,
                () => value ?? new TestMetadata(),
                () => metadata ?? new List<TestMetadata>(),
                () => assertions ?? new List<string>(),
                () => new HigherOrderResultDescription<TestUnderlyingMetadata>(reason ?? "", causes ?? [], ""),
                underlyingResults ?? new List<BooleanResultBase<TestUnderlyingMetadata>>(),
                () => causes ?? new List<BooleanResultBase<TestUnderlyingMetadata>>());
        }

        [Theory, AutoData]
        public void Constructor_ShouldSetSatisfiedProperty(bool isSatisfied)
        {
            // Arrange & Act
            var result = CreateHigherOrderPolicyResult(isSatisfied: isSatisfied);

            // Assert
            result.Satisfied.ShouldBe(isSatisfied);
        }

        [Theory, AutoData]
        public void Value_ShouldReturnValueFromValueFunc(TestMetadata value)
        {
            // Arrange
            var result = CreateHigherOrderPolicyResult(value: value);

            // Act
            var resultValue = result.Value;

            // Assert
            resultValue.ShouldBe(value);
        }

        [Theory, AutoData]
        public void MetadataTier_ShouldReturnMetadataFromMetadataFunc(List<TestMetadata> metadata)
        {
            // Arrange
            var result = CreateHigherOrderPolicyResult(metadata: metadata);

            // Act
            var resultMetadata = result.MetadataTier.Metadata;

            // Assert
            resultMetadata.ShouldBe(metadata);
        }

        [Theory, AutoData]
        public void Explanation_ShouldReturnExplanationWithCorrectAssertions(List<string> assertions)
        {
            // Arrange
            var result = CreateHigherOrderPolicyResult(assertions: assertions);

            // Act
            var explanation = result.Explanation;

            // Assert
            explanation.Assertions.ShouldBe(assertions);
        }

        [Theory, AutoData]
        public void Underlying_ShouldReturnUnderlyingResults(List<TestBooleanResult<TestUnderlyingMetadata>> underlyingResults)
        {
            // Arrange
            var result = CreateHigherOrderPolicyResult(underlyingResults: underlyingResults);

            // Act
            var resultUnderlying = result.Underlying;

            // Assert
            resultUnderlying.ShouldBe(underlyingResults);
        }

        [Fact]
        public void UnderlyingWithValues_ShouldReturnEmptyWhenTypesDoNotMatch()
        {
            // Arrange
            var result = CreateHigherOrderPolicyResult(
                underlyingResults: new List<BooleanResultBase<TestUnderlyingMetadata>> { new TestBooleanResult<TestUnderlyingMetadata>() });

            // Act
            var underlyingWithValues = result.UnderlyingWithValues;

            // Assert
            underlyingWithValues.ShouldBeEmpty();
        }

        [Fact]
        public void Causes_ShouldReturnCausesFromCausesFunc()
        {
            // Arrange
            var causes = new List<BooleanResultBase<TestUnderlyingMetadata>> { new TestBooleanResult<TestUnderlyingMetadata>() };
            var result = CreateHigherOrderPolicyResult(causes: causes);

            // Act
            var resultCauses = result.Causes;

            // Assert
            resultCauses.ShouldBe(causes);
        }

        [Fact]
        public void CausesWithValues_ShouldReturnEmptyWhenTypesDoNotMatch()
        {
            // Arrange
            var causes = new List<BooleanResultBase<TestUnderlyingMetadata>> { new TestBooleanResult<TestUnderlyingMetadata>() };
            var result = CreateHigherOrderPolicyResult(causes: causes);

            // Act
            var causesWithValues = result.CausesWithValues;

            // Assert
            causesWithValues.ShouldBeEmpty();
        }

        [Theory, AutoData]
        public void Description_ShouldReturnHigherOrderResultDescriptionWithCorrectReason(string reason)
        {
            // Arrange
            var result = CreateHigherOrderPolicyResult(reason: reason);

            // Act
            var description = result.Description as HigherOrderResultDescription<TestUnderlyingMetadata>;

            // Assert
            description.ShouldNotBeNull();
            description.Reason.ShouldBe(reason);
        }
    }

    public class TestBooleanResult<T> : BooleanResultBase<T>
    {
        public override MetadataNode<T> MetadataTier => throw new NotImplementedException();
        public override Explanation Explanation => throw new NotImplementedException();
        public override IEnumerable<BooleanResultBase> Underlying => throw new NotImplementedException();
        public override IEnumerable<BooleanResultBase<T>> UnderlyingWithValues => throw new NotImplementedException();
        public override IEnumerable<BooleanResultBase> Causes => throw new NotImplementedException();
        public override IEnumerable<BooleanResultBase<T>> CausesWithValues => throw new NotImplementedException();
        public override bool Satisfied => throw new NotImplementedException();
        public override ResultDescriptionBase Description => throw new NotImplementedException();
    }
