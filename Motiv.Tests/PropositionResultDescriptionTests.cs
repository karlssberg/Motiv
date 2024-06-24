namespace Motiv.Tests;

public class PropositionResultDescriptionTests
{
    [Theory]
    [InlineAutoData(true, "is true")]
    [InlineAutoData(false, "is false")]
    public void Should_generate_a_simple_description_reason(bool isTrue, string expected)
    {
        // Arrange
        var spec = Spec
            .Build<object>(_ => isTrue)
            .WhenTrue("is true")
            .WhenFalse("is false")
            .Create();

        var result = spec.IsSatisfiedBy(new object());

        // Act
        var act = result.Reason;
        
        // Assert
        act.Should().BeEquivalentTo(expected);
    }
    
    [Theory]
    [InlineAutoData(true, "is true")]
    [InlineAutoData(false, "is false")]
    public void Should_generate_a_simple_description_reason_regardless_of_the_proposition_name(bool isTrue, string expected)
    {
        // Arrange
        var spec = Spec
            .Build<object>(_ => isTrue)
            .WhenTrue("is true")
            .WhenFalse("is false")
            .Create("is true proposition");

        var result = spec.IsSatisfiedBy(new object());

        // Act
        var act = result.Reason;
        
        // Assert
        act.Should().BeEquivalentTo(expected);
    }  
    
    [Theory]
    [InlineAutoData(true, "is true")]
    [InlineAutoData(false, "!is true")]
    public void Should_generate_a_simple_description_using_proposition_when_metadata_is_not_a_string(
        bool isTrue,
        string expected,
        object model)
    {
        // Arrange
        var spec = Spec
            .Build<object>(_ => isTrue)
            .WhenTrue(true)
            .WhenFalse(false)
            .Create("is true");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Reason;
        
        // Assert
        act.Should().Be(expected);
    }
    
    [Theory]
    [InlineAutoData(false, false, "!left is true | !right is true")]
    [InlineAutoData(false, true, "!left is true & right is true")]
    [InlineAutoData(true, false, "left is true & !right is true")]
    [InlineAutoData(true, true, "right is true | left is true")]
    public void Should_generate_a_description_from_a_composition(
        bool leftResult,
        bool rightResult,
        string expected,
        bool model)
    {
        // Arrange
        var left = Spec
            .Build<bool>(_ => leftResult)
            .Create("left is true");
        
        var right = Spec
            .Build<bool>(_ => rightResult)
            .Create("right is true");
        
        var spec = (left & !right) | (!left & right);

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Reason;
        
        // Assert
        act.Should().Be(expected);
    }
    
    [Theory]
    [InlineAutoData(false, false, false, false, "(!first | !second) & (!third | !fourth)")]
    [InlineAutoData(false, false, false, true,  "!first | !second")]
    [InlineAutoData(false, false, true, false,  "!first | !second")]
    [InlineAutoData(false, false, true, true,   "!first | !second")]
    [InlineAutoData(false, true, false, false,  "!third | !fourth")]
    [InlineAutoData(false, true, false, true,   "second & fourth")]
    [InlineAutoData(false, true, true, false,   "second & third")]  
    [InlineAutoData(false, true, true, true,    "second & (third | fourth)")]
    [InlineAutoData(true, false, false, false,  "!third | !fourth")]
    [InlineAutoData(true, false, false, true,   "first & fourth")]
    [InlineAutoData(true, false, true, false,   "first & third")]
    [InlineAutoData(true, false, true, true,    "first & (third | fourth)")]
    [InlineAutoData(true, true, false, false,   "!third | !fourth")]
    [InlineAutoData(true, true, false, true,    "(first | second) & fourth")]
    [InlineAutoData(true, true, true, false,    "(first | second) & third")]
    [InlineAutoData(true, true, true, true,     "(first | second) & (third | fourth)")]
    public void Should_generate_a_description_from_a_complicated_composition_using_propositions(
        bool firstValue,
        bool secondValue,
        bool thirdValue,
        bool fourthValue,
        string expected,
        bool model)
    {
        // Arrange
        var first = Spec
            .Build<bool>(_ => firstValue)
            .Create("first");
        
        var second = Spec
            .Build<bool>(_ => secondValue)
            .Create("second");
        
        var third = Spec
            .Build<bool>(_ => thirdValue)
            .Create("third");
        
        var fourth = Spec
            .Build<bool>(_ => fourthValue)
            .Create("fourth");
        
        var spec = (first | second) & (third | fourth);
        
        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Reason;
        
        // Assert
        act.Should().Be(expected);
    }
    
    [Theory]
    [InlineAutoData(false, false, false, false, "(not first | not second) & (not third | not fourth)")]
    [InlineAutoData(false, false, false, true,  "not first | not second")]
    [InlineAutoData(false, false, true, false,  "not first | not second")]
    [InlineAutoData(false, false, true, true,   "not first | not second")]
    [InlineAutoData(false, true, false, false,  "not third | not fourth")]
    [InlineAutoData(false, true, false, true,   "is second & is fourth")]
    [InlineAutoData(false, true, true, false,   "is second & is third")]
    [InlineAutoData(false, true, true, true,    "is second & (is third | is fourth)")]
    [InlineAutoData(true, false, false, false,  "not third | not fourth")]
    [InlineAutoData(true, false, false, true,   "is first & is fourth")]
    [InlineAutoData(true, false, true, false,   "is first & is third")]
    [InlineAutoData(true, false, true, true,    "is first & (is third | is fourth)")]
    [InlineAutoData(true, true, false, false,   "not third | not fourth")]
    [InlineAutoData(true, true, false, true,    "(is first | is second) & is fourth")]
    [InlineAutoData(true, true, true, false,    "(is first | is second) & is third")]
    [InlineAutoData(true, true, true, true,     "(is first | is second) & (is third | is fourth)")]
    public void Should_generate_a_description_from_a_complicated_composition(
        bool firstValue,
        bool secondValue,
        bool thirdValue,
        bool fourthValue,
        string expected,
        bool model)
    {
        // Arrange
        var first = Spec
            .Build<bool>(_ => firstValue)
            .WhenTrue("is first")
            .WhenFalse("not first")
            .Create();
        
        var second = Spec
            .Build<bool>(_ => secondValue)
            .WhenTrue("is second")
            .WhenFalse("not second")
            .Create();
        
        var third = Spec
            .Build<bool>(_ => thirdValue)
            .WhenTrue("is third")
            .WhenFalse("not third")
            .Create();
        
        var fourth = Spec
            .Build<bool>(_ => fourthValue)
            .WhenTrue("is fourth")
            .WhenFalse("not fourth")
            .Create();
        
        var spec = (first | second) & (third | fourth);
        
        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Reason;
        
        // Assert
        act.Should().Be(expected);
    }
    
    [Theory]
    [InlineAutoData(false, false, false, false, 
        """
        some are false
            AND
                OR
                    !first
                    !second
                OR
                    !third
                    !fourth
        """)]
    [InlineAutoData(false, false, false, true,
        """
        some are false
            AND
                OR
                    !first
                    !second
        """)]
    [InlineAutoData(false, false, true, false,
        """
        some are false
            AND
                OR
                    !first
                    !second
        """)]
    [InlineAutoData(false, false, true, true,
        """
        some are false
            AND
                OR
                    !first
                    !second
        """)]
    [InlineAutoData(false, true, false, false,
        """
        some are false
            AND
                OR
                    !third
                    !fourth
        """)]
    [InlineAutoData(false, true, false, true,
        """
        all are true
            AND
                OR
                    second
                OR
                    fourth
        """)]
    [InlineAutoData(false, true, true, false,
        """
        all are true
            AND
                OR
                    second
                OR
                    third
        """)]
    [InlineAutoData(false, true, true, true,
        """
        all are true
            AND
                OR
                    second
                OR
                    third
                    fourth
        """)]
    [InlineAutoData(true, false, false, false,
        """
        some are false
            AND
                OR
                    !third
                    !fourth
        """)]
    [InlineAutoData(true, false, false, true,
        """
        all are true
            AND
                OR
                    first
                OR
                    fourth
        """)]
    [InlineAutoData(true, false, true, false,
        """
        all are true
            AND
                OR
                    first
                OR
                    third
        """)]
    [InlineAutoData(true, false, true, true,
        """
        all are true
            AND
                OR
                    first
                OR
                    third
                    fourth
        """)]
    [InlineAutoData(true, true, false, false,
        """
        some are false
            AND
                OR
                    !third
                    !fourth
        """)]
    [InlineAutoData(true, true, false, true,
        """
        all are true
            AND
                OR
                    first
                    second
                OR
                    fourth
        """)]
    [InlineAutoData(true, true, true, false,
        """
        all are true
            AND
                OR
                    first
                    second
                OR
                    third
        """)]
    [InlineAutoData(true, true, true, true,
        """
        all are true
            AND
                OR
                    first
                    second
                OR
                    third
                    fourth
        """)]
    public void Should_generate_a_description_from_a_complicated_composition_of_higher_order_spec(
        bool firstValue,
        bool secondValue,
        bool thirdValue,
        bool fourthValue,  
        string expected)
    {
        // Arrange
        var first = Spec
            .Build<bool>(val => firstValue & val)
            .Create("first");
        
        var second = Spec
            .Build<bool>(val => secondValue & val)
            .Create("second");
        
        var third = Spec
            .Build<bool>(val => thirdValue & val)
            .Create("third");
        
        var fourth = Spec
            .Build<bool>(val => fourthValue & val)
            .Create("fourth");
        
        var underlying = (first | second) & (third | fourth);
        var spec = Spec
            .Build(underlying)
            .AsAllSatisfied()
            .WhenTrue("all are true")
            .WhenFalse("some are false")
            .Create();
        
        var result = spec.IsSatisfiedBy([true]);

        // Act
        var act = result.Justification;
        
        // Assert
        act.Should().Be(expected);
    }
    
    [Theory]
    [InlineAutoData(false, false, false, false, 
        """
        AND
            OR
                !fourth
        """)]
    [InlineAutoData(false, false, false, true,
        """
        AND
            OR
                !second
            OR
                !third
                fourth
        """)]
    [InlineAutoData(false, false, true, false,
        """
        AND
            OR
                third
                !fourth
        """)]
    [InlineAutoData(false, false, true, true,
        """
        AND
            OR
                third
        """)]
    [InlineAutoData(false, true, false, false,
        """
        AND
            OR
                !first
                second
            OR
                !fourth
        """)]
    [InlineAutoData(false, true, false, true,
        """
        AND
            OR
                !first
                second
        """)]
    [InlineAutoData(false, true, true, false,
        """
        AND
            OR
                !first
                second
            OR
                third
                !fourth
        """)]
    [InlineAutoData(false, true, true, true,
        """
        AND
            OR
                !first
                second
            OR
                third
        """)]
    [InlineAutoData(true, false, false, false,
        """
        AND
            OR
                !fourth
        """)]
    [InlineAutoData(true, false, false, true,
        """
        AND
            OR
                first
                !second
            OR
                !third
                fourth
        """)]
    [InlineAutoData(true, false, true, false,
        """
        AND
            OR
                third
                !fourth
        """)]
    [InlineAutoData(true, false, true, true,
        """
        AND
            OR
                third
        """)]
    [InlineAutoData(true, true, false, false,
        """
        AND
            OR
                !fourth
        """)]
    [InlineAutoData(true, true, false, true,
        """
        AND
            OR
                first
            OR
                !third
                fourth
        """)]
    [InlineAutoData(true, true, true, false,
        """
        AND
            OR
                third
                !fourth
        """)]
    [InlineAutoData(true, true, true, true,
        """
        AND
            OR
                third
        """)]
    public void Should_generate_a_detailed_description_from_a_complicated_composition_of_a_first_order_expression(
        bool firstValue,
        bool secondValue,
        bool thirdValue,
        bool fourthValue,  
        string expected)
    {
        // Arrange
        var first = Spec
            .Build<bool>(val => firstValue & val)
            .Create("first");
        
        var second = Spec
            .Build<bool>(val => secondValue & val)
            .Create("second");
        
        var third = Spec
            .Build<bool>(val => thirdValue & val)
            .Create("third");
        
        var fourth = Spec
            .Build<bool>(val => fourthValue & val)
            .Create("fourth");
        
        var spec = (first | !second) & !(third | !fourth);
        var result = spec.IsSatisfiedBy(true);

        // Act
        var act = result.Justification;
        
        // Assert
        act.Should().Be(expected);
    }
    
    [Theory]
    [AutoData]
    public void Should_use_the_compact_description_as_the_toString_method(object model)
    {
        // Arrange
        var spec = Spec
            .Build<object>(_ => true)
            .WhenTrue("is true")
            .WhenFalse("is false")
            .Create("always true");

        var result = spec.IsSatisfiedBy(model);

        var act = result.Description.ToString();
        
        // Assert
        act.Should().Be(result.Description.Reason);
    }

    [Fact]
    public void Should_collapse_operators_in_spec_description_to_improve_readability()
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
        
        var fifth = Spec
            .Build<bool>(_ => true)
            .Create("fifth");
        
        var sixth = Spec
            .Build<bool>(_ => true)
            .Create("sixth");
        
        var seventh = Spec
            .Build<bool>(_ => true)
            .Create("seventh");
        
        var spec = !(first & second).AndAlso((third | fourth) & !(fifth | !sixth) & !!!!seventh);

        // Act
        var act = spec.Expression;
        
        // Assert
        act.Should().Be(
            """
            !AND ALSO
                AND
                    first
                    second
                AND
                    OR
                        third
                        fourth
                    !OR
                        fifth
                        !sixth
                    seventh
            """);
    }
    
    [Fact]
    public void Should_not_collapse_xor_operators_in_spec_description()
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

        // Act
        var act = spec.Expression;
        
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
    public void Should_collapse_AND_and_ANDALSO_operators_in_spec_description()
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


        var spec = first.AndAlso(second & third & fourth);

        // Act
        var act = spec.Expression;
        
        // Assert
        act.Should().Be(
            """
            AND ALSO
                first
                AND
                    second
                    third
                    fourth
            """);
    }
    
    [Fact]
    public void Should_collapse_OR_and_ORELSE_operators_in_spec_description()
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

        var spec = first.OrElse(second | third | fourth);

        // Act
        var act = spec.Expression;
        
        // Assert
        act.Should().Be(
            """
            OR ELSE
                first
                OR
                    second
                    third
                    fourth
            """);
    }
    
    [Fact]
    public void Should_collapse_operators_in_spec_result_description_to_improve_readability()
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
        
        var fifth = Spec
            .Build<bool>(_ => true)
            .Create("fifth");
        
        var sixth = Spec
            .Build<bool>(_ => true)
            .Create("sixth");
        
        var seventh = Spec
            .Build<bool>(_ => true)
            .Create("seventh");
        
        var spec = (first & second).AndAlso((third | fourth) & (fifth | sixth) & seventh);
        var result = spec.IsSatisfiedBy(true);

        // Act
        var act = result.Justification;
        
        // Assert
        act.Should().Be(
            """
            AND
                first
                second
                OR
                    third
                    fourth
                OR
                    fifth
                    sixth
                seventh
            """);
    }
    
    [Fact]
    public void Should_collapse_AND_and_ANDALSO_operators_in_spec_result_description()
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


        var spec = first.AndAlso(second & third & fourth); 
        var result = spec.IsSatisfiedBy(true);

        // Act
        var act = result.Justification;
        
        // Assert
        act.Should().Be(
            """
            AND
                first
                second
                third
                fourth
            """);
    }
    
    [Fact]
    public void Should_collapse_OR_and_ORELSE_operators_in_spec_result_description()
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

        var spec = first | (second | third | fourth); 
        var result = spec.IsSatisfiedBy(true);

        // Act
        var act = result.Justification;
        
        // Assert
        act.Should().Be(
            """
            OR
                first
                second
                third
                fourth
            """);
    }
}