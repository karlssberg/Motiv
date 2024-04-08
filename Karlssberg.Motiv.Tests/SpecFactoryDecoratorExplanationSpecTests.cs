using FluentAssertions;

namespace Karlssberg.Motiv.Tests;

public class SpecFactoryDecoratorExplanationSpecTests
{
    
    [InlineAutoData(true, "true after - A", "true after + model - B", "true after - C", "true after + model - D")]
    [InlineAutoData(false, "false after - A", "false after - B", "false after + model - C", "false after + model - D")]
    [Theory]
    public void Should_replace_the_assertions_with_new_assertions(
        bool isSatisfied,
        string expectedA,
        string expectedB,
        string expectedC,
        string expectedD)
    {
        string[] expectation = [expectedA, expectedB, expectedC, expectedD];
        var underlying = Spec
            .Build<string>(_ => isSatisfied)
            .WhenTrue("true before")
            .WhenFalse("false before")
            .Create();

        var firstSpec = Spec
            .Build(() => underlying)
            .WhenTrue("true after - A")
            .WhenFalse("false after - A")
            .Create();

        var secondSpec = Spec
            .Build(() => underlying)
            .WhenTrue(model => $"true after + {model} - B")
            .WhenFalse("false after - B")
            .Create("is second true");

        var thirdSpec = Spec
            .Build(() => underlying)
            .WhenTrue("true after - C")
            .WhenFalse(model => $"false after + {model} - C")
            .Create();

        var fourthSpec = Spec
            .Build(() => underlying)
            .WhenTrue(model => $"true after + {model} - D")
            .WhenFalse(model => $"false after + {model} - D")
            .Create("true after + model - D");

        var sut = firstSpec | secondSpec | thirdSpec | fourthSpec;

        var act = sut.IsSatisfiedBy("model");

        act.Assertions.Should().BeEquivalentTo(expectation);
        act.MetadataTree.Should().BeEquivalentTo(expectation);
    }
    
    
    
    [InlineAutoData(true, "true after - A", "true after + model - B", "true after - C", "true after + model - D")]
    [InlineAutoData(false, "false after - A", "false after - B", "false after + model - C", "false after + model - D")]
    [Theory]
    public void Should_replace_the_assertions_collection_with_new_assertions_collection(
        bool isSatisfied,
        string expectedA,
        string expectedB,
        string expectedC,
        string expectedD)
    {
        string[] expectation = [expectedA, expectedB, expectedC, expectedD];
        var underlying = Spec
            .Build<string>(_ => isSatisfied)
            .WhenTrue("true before")
            .WhenFalse("false before")
            .Create();

        var firstSpec = Spec
            .Build(() => underlying)
            .WhenTrue("true after - A")
            .WhenFalse("false after - A")
            .Create();

        var secondSpec = Spec
            .Build(() => underlying)
            .WhenTrue((model, _) => $"true after + {model} - B".ToEnumerable())
            .WhenFalse("false after - B")
            .Create("is second true");

        var thirdSpec = Spec
            .Build(() => underlying)
            .WhenTrue("true after - C")
            .WhenFalse((model, _)  => $"false after + {model} - C".ToEnumerable())
            .Create();

        var fourthSpec = Spec
            .Build(() => underlying)
            .WhenTrue((model, _)  => $"true after + {model} - D".ToEnumerable())
            .WhenFalse((model, _)  => $"false after + {model} - D".ToEnumerable())
            .Create("true after + model - D");

        var sut = firstSpec | secondSpec | thirdSpec | fourthSpec;

        var act = sut.IsSatisfiedBy("model");

        act.Assertions.Should().BeEquivalentTo(expectation);
        act.MetadataTree.Should().BeEquivalentTo(expectation);
    }
    
    [Theory]
    [InlineAutoData(true, "True True True")]
    [InlineAutoData(false, "False False False")]
    public void Should_map_existing_assertions_to_new_metadata(bool model, string expected)
    {
        var underlying = Spec
            .Build((bool m) => m)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create("is underlying true");

        var sut = Spec
            .Build(() => underlying)
            .WhenTrue((boolModel, result) => $"{true.ToString()} {boolModel} {result}")
            .WhenFalse((boolModel, result) => $"{false.ToString()} {boolModel} {result}")
            .Create("is true");
        
        var act = sut.IsSatisfiedBy(model);
        
        act.Assertions.Should().BeEquivalentTo(expected);
    }
    
    [Theory]
    [InlineAutoData(true, "True", "is true")]
    [InlineAutoData(false, "False", "!is true")]
    public void Should_map_underlying_true_assertions_to_new_ones(bool model, string expectedAssertion, string expectedReason)
    {
        var underlying = Spec
            .Build((bool m) => m)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create("is underlying true");

        var sut = Spec
            .Build(() => underlying)
            .WhenTrue((boolModel, result) => result.Assertions.Append(boolModel.ToString()))
            .WhenFalse("False")
            .Create("is true");
        
        var act = sut.IsSatisfiedBy(model);
        
        act.Assertions.Should().BeEquivalentTo([expectedAssertion]);
        act.Reason.Should().Be(expectedReason);
    }
    
    [Theory]
    [InlineAutoData(true, "True", "is true")]
    [InlineAutoData(false, "False", "!is true")]
    public void Should_map_underlying_false_assertions_to_new_ones(bool model, string expectedAssertion, string expectedReason)
    {
        var underlying = Spec
            .Build((bool m) => m)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create("is underlying true");

        var sut = Spec
            .Build(() => underlying)
            .WhenTrue("True")
            .WhenFalse((boolModel, result) => result.Assertions.Append(boolModel.ToString()))
            .Create("is true");
        
        var act = sut.IsSatisfiedBy(model);
        
        act.Assertions.Should().BeEquivalentTo([expectedAssertion]);
        act.Reason.Should().Be(expectedReason);
    }
}