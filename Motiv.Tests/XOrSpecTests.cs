using System.Text.RegularExpressions;

namespace Motiv.Tests;

public class XOrSpecTests
{
    [Theory]
    [InlineAutoData(true, true, false)]
    [InlineAutoData(true, false, true)]
    [InlineAutoData(false, true, true)]
    [InlineAutoData(false, false, false)]
    public void Should_perform_logical_xor(
        bool leftResult,
        bool rightResult,
        bool expected,
        object model)
    {
        // Arrange
        var left = Spec
            .Build<object>(_ => leftResult)
            .WhenTrue(true)
            .WhenFalse(false)
            .Create("left");

        var right = Spec
            .Build<object>(_ => rightResult)
            .WhenTrue(true)
            .WhenFalse(false)
            .Create("right");

        var spec = left ^ right;

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Satisfied;
        
        // Assert
        act.Should().Be(expected);
    }
    
    [Theory]
    [InlineAutoData(true, true, false)]
    [InlineAutoData(true, false, true)]
    [InlineAutoData(false, true, true)]
    [InlineAutoData(false, false, false)]
    public void Should_yield_metadata(
        bool leftResult,
        bool rightResult,
        bool expected,
        object model)
    {
        // Arrange
        var left = Spec
            .Build<object>(_ => leftResult)
            .WhenTrue(true)
            .WhenFalse(false)
            .Create("left");

        var right = Spec
            .Build<object>(_ => rightResult)
            .WhenTrue(true)
            .WhenFalse(false)
            .Create("right");

        var spec = left ^ right;

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Metadata;
        
        // Assert
        act.Should().HaveCount(leftResult == rightResult ? 1 : 2)
            .And.Contain(leftResult)
            .And.Contain(rightResult);
    }
    
    [Theory]
    [InlineAutoData(true, true, "left ^ right")]
    [InlineAutoData(true, false, "left ^ !right")]
    [InlineAutoData(false, true, "!left ^ right")]  
    [InlineAutoData(false, false, "!left ^ !right")]
    public void Should_serialize_the_result_of_the_xor_operation(
        bool leftResult,
        bool rightResult,
        string expected,
        object model)
    {
        // Arrange
        var left = Spec
            .Build<object>(_ => leftResult)
            .WhenTrue(true)
            .WhenFalse(false)
            .Create("left");

        var right = Spec
            .Build<object>(_ => rightResult)
            .WhenTrue(true)
            .WhenFalse(false)
            .Create("right");

        var spec = left ^ right;

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Reason;
        
        // Assert
        act.Should().Be(expected);
    }
    
    [Theory]
    [InlineAutoData(true, true, "none")]
    [InlineAutoData(true, false, "left")]
    [InlineAutoData(false, true, "right")]  
    [InlineAutoData(false, false, "none")]
    public void Should_be_able_to_override_the_assertions_to_only_the_true_operand_has_its_output(
        bool leftResult,
        bool rightResult,
        string expected,
        object model)
    {
        // Arrange
        var left = Spec
            .Build<object>(_ => leftResult)
            .WhenTrue(true)
            .WhenFalse(false)
            .Create("left");

        var right = Spec
            .Build<object>(_ => rightResult)
            .WhenTrue(true)
            .WhenFalse(false)
            .Create("right");

        var spec = Spec
            .Build(left ^ right)
            .WhenTrue((_, result) => result.Causes.GetTrueAssertions())
            .WhenFalse("none")
            .Create("xor");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Assertions;
        
        // Assert
        act.Should().BeEquivalentTo(expected);
    }

    [Theory]
    [InlineAutoData(true, true, "True ^ True")]
    [InlineAutoData(true, false, "True ^ False")]
    [InlineAutoData(false, true, "False ^ True")]
    [InlineAutoData(false, false, "False ^ False")]
    public void Should_serialize_the_result_of_the_xor_operation_when_metadata_is_a_string(
        bool leftResult,
        bool rightResult,
        string expected,
        object model)
    {
        // Arrange
        var left = Spec
            .Build<object>(_ => leftResult)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create();

        var right = Spec
            .Build<object>(_ => rightResult)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create();

        var spec = left ^ right;

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Reason;
        
        // Assert
        act.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(true, true, "True ^ True")]
    [InlineAutoData(true, false, "True ^ False")]
    [InlineAutoData(false, true, "False ^ True")]
    [InlineAutoData(false, false, "False ^ False")]
    public void Should_serializeing_when_using_the_single_generic_specification_type(
        bool leftResult,
        bool rightResult,
        string expected,
        object model)
    {
        // Arrange
        var left = Spec
            .Build<object>(_ => leftResult)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create();

        var right = Spec
            .Build<object>(_ => rightResult)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create();

        var spec = left ^ right;

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Reason;
        
        // Assert
        act.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(true, true)]
    [InlineAutoData(true, false)]
    [InlineAutoData(false, true)]
    [InlineAutoData(false, false)]
    public void Should_provide_a_statement_about_the_specification(bool leftResult, bool rightResult)
    {
        // Arrange
        var left = Spec
            .Build<object>(_ => leftResult)
            .WhenTrue(true)
            .WhenFalse(false)
            .Create("left");

        var right = Spec
            .Build<object>(_ => rightResult)
            .WhenTrue(true)
            .WhenFalse(false)
            .Create("right");

        var expected = $"{left.Statement} ^ {right.Statement}";

        var spec = left ^ right;

        // Act
        var act = spec.Statement;
        
        // Assert
        act.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(true, true)]
    [InlineAutoData(true, false)]
    [InlineAutoData(false, true)]
    [InlineAutoData(false, false)]
    public void Should_serialize_specification(bool leftResult, bool rightResult)
    {
        // Arrange
        var left = Spec
            .Build<object>(_ => leftResult)
            .WhenTrue(true)
            .WhenFalse(false)
            .Create("left");

        var right = Spec
            .Build<object>(_ => rightResult)
            .WhenTrue(true)
            .WhenFalse(false)
            .Create("right");

        var expected = $"{left.Statement} ^ {right.Statement}";

        var spec = left ^ right;

        // Act
        var act = spec.ToString();
        
        // Assert
        act.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(true, true)]
    [InlineAutoData(true, false)]
    [InlineAutoData(false, true)]
    [InlineAutoData(false, false)]
    public void Should_implicitly_obtain_the_statement_from_the_assertion(bool leftResult, bool rightResult)
    {
        // Arrange
        var left = Spec
            .Build<object>(_ => leftResult)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create();

        var right = Spec
            .Build<object>(_ => rightResult)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create();

        var expected = $"{left.Statement} ^ {right.Statement}";

        var spec = left ^ right;

        // Act
        var act = spec.Statement;
        
        // Assert
        act.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, 2)]
    [InlineAutoData(false, true, 2)]
    [InlineAutoData(true, false, 2)]
    [InlineAutoData(true, true, 2)]
    public void Should_accurately_report_the_number_of_causal_operands(bool left, bool right, int expected,
        object model)
    {
        // Arrange
        var leftSpec = Spec
            .Build<object>(_ => left)
            .Create("left");

        var rightSpec = Spec
            .Build<object>(_ => right)
            .Create("right");

        var spec = leftSpec ^ rightSpec;

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Description.CausalOperandCount;
        
        // Assert
        act.Should().Be(expected);
    }
    
    [Theory]
    [InlineAutoData(false, false, false)]
    [InlineAutoData(false, true, true)]
    [InlineAutoData(true, false, true)]
    [InlineAutoData(true, true, false)]
    public void Should_perform_OrElse_on_specs_with_different_metadata(
        bool leftValue,
        bool rightValue,
        bool expectedSatisfied,
        Guid leftTrue,
        Guid leftFalse,
        int  rightTrue,
        int  rightFalse)
    {
        // Arrange
        var left =
            Spec.Build((string _) => leftValue)
                .WhenTrue(leftTrue)
                .WhenFalse(leftFalse)
                .Create("left");

        var right =
            Spec.Build((string _) => rightValue)
                .WhenTrue(rightTrue)
                .WhenFalse(rightFalse)
                .Create("right");

        var spec = left ^ right;
        
        var result = spec.IsSatisfiedBy("");

        // Act
        var act = result.Satisfied;
        
        // Assert
        act.Should().Be(expectedSatisfied);
    }
    
    [Theory]
    [InlineData(false, false, "!left", "!right")]
    [InlineData(false, true, "!left", "right")]
    [InlineData(true, false, "left", "!right")]
    [InlineData(true, true, "left", "right")]
    public void Should_perform_OrElse_on_specs_with_different_metadata_and_preserve_assertions(
        bool leftValue,
        bool rightValue,
        params string[] expected)
    {
        // Arrange
        var left =
            Spec.Build((string _) => leftValue)
                .WhenTrue(new Uri("http://true"))
                .WhenFalse(new Uri("http://false"))
                .Create("left");

        var right =
            Spec.Build((string _) => rightValue)
                .WhenTrue(new Regex("true"))
                .WhenFalse(new Regex("false"))
                .Create("right");

        var spec = left ^ right;
        
        var result = spec.IsSatisfiedBy("");

        // Act
        var act = result.Assertions;
        
        // Assert
        act.Should().BeEquivalentTo(expected);
    }
    
    [Theory]
    [InlineData(false, false, "!left", "!right")]
    [InlineData(false, true, "!left", "right")]
    [InlineData(true, false, "left", "!right")]
    [InlineData(true, true, "left", "right")]
    public void Should_yield_metadata_as_a_string_when_specs_containing_different_metadata_types_are_composed(
        bool leftValue,
        bool rightValue,
        params string[] expected)
    {
        // Arrange
        var left =
            Spec.Build((string _) => leftValue)
                .WhenTrue(new Uri("http://true"))
                .WhenFalse(new Uri("http://false"))
                .Create("left");

        var right =
            Spec.Build((string _) => rightValue)
                .WhenTrue(new Regex("true"))
                .WhenFalse(new Regex("false"))
                .Create("right");

        var spec = left ^ right;
        
        var result = spec.IsSatisfiedBy("");

        // Act
        var act = result.Metadata;
        
        // Assert
        act.Should().BeEquivalentTo(expected);
    }
    
    [Fact]
    public void Should_not_collapse_xor_operators_in_spec_result_description()
    {
        // Arrange
        var first = Spec
            .Build<bool>(_ => true)
            .Create("first");
        
        var second = Spec
            .Build<bool>(_ => true)
            .Create("second");
        
        var third = Spec
            .Build<bool>(_ => true)
            .Create("third");
        
        var fourth = Spec
            .Build<bool>(_ => true)
            .Create("fourth");

        var spec = first ^ second ^ third ^ fourth; 
        var result = spec.IsSatisfiedBy(true);

        // Act
        var act = result.Justification;
        
        // Assert
        act.Should().Be(
            """
            XOR
                XOR
                    XOR
                        first
                        second
                    third
                fourth
            """);
    }

    [Fact]
    public void Should_not_collapse_xor_operators_in_spec_result_description_when_grouped()
    {
        // Arrange
        var first = Spec
            .Build<bool>(_ => true)
            .Create("first");

        var second = Spec
            .Build<bool>(_ => true)
            .Create("second");

        var third = Spec
            .Build<bool>(_ => true)
            .Create("third");

        var fourth = Spec
            .Build<bool>(_ => true)
            .Create("fourth");


        var spec = first ^ second ^ (third ^ fourth);
        
        var result = spec.IsSatisfiedBy(true);

        // Act
        var act = result.Justification;
        
        // Assert
        act.Should().Be(
            """
            XOR
                XOR
                    first
                    second
                XOR
                    third
                    fourth
            """);
    }
    
    [Fact]
    public void Should_not_collapse_xor_operators_in_spec_result_description_when_grouped_in_reverse_order()
    {
        // Arrange
        var first = Spec
            .Build<bool>(_ => true)
            .Create("first");
        
        var second = Spec
            .Build<bool>(_ => true)
            .Create("second");
        
        var third = Spec
            .Build<bool>(_ => true)
            .Create("third");
        
        var fourth = Spec
            .Build<bool>(_ => true)
            .Create("fourth");

        var spec = first ^ (second ^ (third ^ fourth)); 
        
        var result = spec.IsSatisfiedBy(true);

        // Act
        var act = result.Justification;
        
        // Assert
        act.Should().Be(
            """
            XOR
                first
                XOR
                    second
                    XOR
                        third
                        fourth
            """);
    }
}