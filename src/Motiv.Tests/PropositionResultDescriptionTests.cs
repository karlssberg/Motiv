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

        var result = spec.Evaluate(new object());

        // Act
        var act = result.Reason;

        // Assert
        act.ShouldBe(expected);
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

        var result = spec.Evaluate(new object());

        // Act
        var act = result.Reason;

        // Assert
        act.ShouldBe(expected);
    }

    [Theory]
    [InlineAutoData(true, "is true == true")]
    [InlineAutoData(false, "is true == false")]
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

        var result = spec.Evaluate(model);

        // Act
        var act = result.Reason;

        // Assert
        act.ShouldBe(expected);
    }

    [Theory]
    [InlineAutoData(false, false, "(left == false) | (right == false)")]
    [InlineAutoData(false, true, "!(left == false) & (right == true)")]
    [InlineAutoData(true, false, "(left == true) & !(right == false)")]
    [InlineAutoData(true, true, "!(right == true) | !(left == true)")]
    public void Should_generate_a_description_from_a_composition(
        bool leftResult,
        bool rightResult,
        string expected,
        bool model)
    {
        // Arrange
        var left = Spec
            .Build<bool>(_ => leftResult)
            .Create("left");

        var right = Spec
            .Build<bool>(_ => rightResult)
            .Create("right");

        var spec = (left & !right) | (!left & right);

        var result = spec.Evaluate(model);

        // Act
        var act = result.Reason;

        // Assert
        act.ShouldBe(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, false, "((first == false) | (second == false)) & ((third == false) | (fourth == false))")]
    [InlineAutoData(false, false, false, true,  "(first == false) | (second == false)")]
    [InlineAutoData(false, false, true, false,  "(first == false) | (second == false)")]
    [InlineAutoData(false, false, true, true,   "(first == false) | (second == false)")]
    [InlineAutoData(false, true, false, false,  "(third == false) | (fourth == false)")]
    [InlineAutoData(false, true, false, true,   "(second == true) & (fourth == true)")]
    [InlineAutoData(false, true, true, false,   "(second == true) & (third == true)")]
    [InlineAutoData(false, true, true, true,    "(second == true) & ((third == true) | (fourth == true))")]
    [InlineAutoData(true, false, false, false,  "(third == false) | (fourth == false)")]
    [InlineAutoData(true, false, false, true,   "(first == true) & (fourth == true)")]
    [InlineAutoData(true, false, true, false,   "(first == true) & (third == true)")]
    [InlineAutoData(true, false, true, true,    "(first == true) & ((third == true) | (fourth == true))")]
    [InlineAutoData(true, true, false, false,   "(third == false) | (fourth == false)")]
    [InlineAutoData(true, true, false, true,    "((first == true) | (second == true)) & (fourth == true)")]
    [InlineAutoData(true, true, true, false,    "((first == true) | (second == true)) & (third == true)")]
    [InlineAutoData(true, true, true, true,     "((first == true) | (second == true)) & ((third == true) | (fourth == true))")]
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

        var result = spec.Evaluate(model);

        // Act
        var act = result.Reason;

        // Assert
        act.ShouldBe(expected);
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

        var result = spec.Evaluate(model);

        // Act
        var act = result.Reason;

        // Assert
        act.ShouldBe(expected);
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
    public void Should_serialize_the_description_from_a_complicated_composition(
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

        var result = spec.Evaluate(model);

        // Act
        var act = result.Description.ToString();

        // Assert
        act.ShouldBe(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, false,
        """
        some are false
            AND (1)
                OR
                    first == false
                    second == false
                OR
                    third == false
                    fourth == false
        """)]
    [InlineAutoData(false, false, false, true,
        """
        some are false
            AND (1)
                OR
                    first == false
                    second == false
        """)]
    [InlineAutoData(false, false, true, false,
        """
        some are false
            AND (1)
                OR
                    first == false
                    second == false
        """)]
    [InlineAutoData(false, false, true, true,
        """
        some are false
            AND (1)
                OR
                    first == false
                    second == false
        """)]
    [InlineAutoData(false, true, false, false,
        """
        some are false
            AND (1)
                OR
                    third == false
                    fourth == false
        """)]
    [InlineAutoData(false, true, false, true,
        """
        all are true
            AND (1)
                OR
                    second == true
                OR
                    fourth == true
        """)]
    [InlineAutoData(false, true, true, false,
        """
        all are true
            AND (1)
                OR
                    second == true
                OR
                    third == true
        """)]
    [InlineAutoData(false, true, true, true,
        """
        all are true
            AND (1)
                OR
                    second == true
                OR
                    third == true
                    fourth == true
        """)]
    [InlineAutoData(true, false, false, false,
        """
        some are false
            AND (1)
                OR
                    third == false
                    fourth == false
        """)]
    [InlineAutoData(true, false, false, true,
        """
        all are true
            AND (1)
                OR
                    first == true
                OR
                    fourth == true
        """)]
    [InlineAutoData(true, false, true, false,
        """
        all are true
            AND (1)
                OR
                    first == true
                OR
                    third == true
        """)]
    [InlineAutoData(true, false, true, true,
        """
        all are true
            AND (1)
                OR
                    first == true
                OR
                    third == true
                    fourth == true
        """)]
    [InlineAutoData(true, true, false, false,
        """
        some are false
            AND (1)
                OR
                    third == false
                    fourth == false
        """)]
    [InlineAutoData(true, true, false, true,
        """
        all are true
            AND (1)
                OR
                    first == true
                    second == true
                OR
                    fourth == true
        """)]
    [InlineAutoData(true, true, true, false,
        """
        all are true
            AND (1)
                OR
                    first == true
                    second == true
                OR
                    third == true
        """)]
    [InlineAutoData(true, true, true, true,
        """
        all are true
            AND (1)
                OR
                    first == true
                    second == true
                OR
                    third == true
                    fourth == true
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

        var result = spec.Evaluate([true]);

        // Act
        var act = result.Justification;

        // Assert
        act.ShouldBe(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, false,
        """
        AND
            NOR
                fourth == false
        """)]
    [InlineAutoData(false, false, false, true,
        """
        AND
            OR
                second == false
            NOR
                third == false
                fourth == true
        """)]
    [InlineAutoData(false, false, true, false,
        """
        AND
            NOR
                third == true
                fourth == false
        """)]
    [InlineAutoData(false, false, true, true,
        """
        AND
            NOR
                third == true
        """)]
    [InlineAutoData(false, true, false, false,
        """
        AND
            OR
                first == false
                second == true
            NOR
                fourth == false
        """)]
    [InlineAutoData(false, true, false, true,
        """
        AND
            OR
                first == false
                second == true
        """)]
    [InlineAutoData(false, true, true, false,
        """
        AND
            OR
                first == false
                second == true
            NOR
                third == true
                fourth == false
        """)]
    [InlineAutoData(false, true, true, true,
        """
        AND
            OR
                first == false
                second == true
            NOR
                third == true
        """)]
    [InlineAutoData(true, false, false, false,
        """
        AND
            NOR
                fourth == false
        """)]
    [InlineAutoData(true, false, false, true,
        """
        AND
            OR
                first == true
                second == false
            NOR
                third == false
                fourth == true
        """)]
    [InlineAutoData(true, false, true, false,
        """
        AND
            NOR
                third == true
                fourth == false
        """)]
    [InlineAutoData(true, false, true, true,
        """
        AND
            NOR
                third == true
        """)]
    [InlineAutoData(true, true, false, false,
        """
        AND
            NOR
                fourth == false
        """)]
    [InlineAutoData(true, true, false, true,
        """
        AND
            OR
                first == true
            NOR
                third == false
                fourth == true
        """)]
    [InlineAutoData(true, true, true, false,
        """
        AND
            NOR
                third == true
                fourth == false
        """)]
    [InlineAutoData(true, true, true, true,
        """
        AND
            NOR
                third == true
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
        var result = spec.Evaluate(true);

        // Act
        var act = result.Justification;

        // Assert
        act.ShouldBe(expected);
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

        var result = spec.Evaluate(model);

        var act = result.Description.ToString();

        // Assert
        act.ShouldBe(result.Description.Reason);
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
        act.ShouldBe(
            """
            NAND ALSO
                AND
                    first
                    second
                AND
                    OR
                        third
                        fourth
                    NOR
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
        act.ShouldBe(
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
        act.ShouldBe(
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
        act.ShouldBe(
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
        var result = spec.Evaluate(true);

        // Act
        var act = result.Justification;

        // Assert
        act.ShouldBe(
            """
            AND ALSO
                AND
                    first == true
                    second == true
                AND
                    OR
                        third == true
                        fourth == true
                    OR
                        fifth == true
                        sixth == true
                    seventh == true
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
        var result = spec.Evaluate(true);

        // Act
        var act = result.Justification;

        // Assert
        act.ShouldBe(
            """
            AND ALSO
                first == true
                AND
                    second == true
                    third == true
                    fourth == true
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
        var result = spec.Evaluate(true);

        // Act
        var act = result.Justification;

        // Assert
        act.ShouldBe(
            """
            OR
                first == true
                second == true
                third == true
                fourth == true
            """);
    }

    [Theory]
    [InlineData(true, true,"!((b == false) | (c == false))", "b == false", "c == false")]
    [InlineData(false, false,"!((b == true) | (c == true))", "b == true", "c == true")]
    public void Should_negate_a_binary_operation_results_that_contains_negated_proposition_statements(
        bool model,
        bool expectedSatisfied,
        string expectedReason,
        params string[] expectedAssertions)
    {
        var specB = Spec.Build((bool b) => !b).Create("b");
        var specC = Spec.Build((bool b) => !b).Create("c");

        var spec =  !(specB | specC);

        var act = spec.Evaluate(model);

        act.Satisfied.ShouldBe(expectedSatisfied);
        act.Reason.ShouldBe(expectedReason);
        act.Assertions.ShouldBe(expectedAssertions);
    }

    [Theory]
    [InlineData(true, false, "!(!(b == false) | !(c == false))", "b == false", "c == false")]
    [InlineData(false, true, "!(!(b == true) | !(c == true))", "b == true", "c == true")]
    public void Should_negate_a_binary_operation_results_that_contains_single_negated_proposition_statements(
        bool model,
        bool expectedSatisfied,
        string expectedReason,
        params string[] expectedAssertions)
    {
        var specB = Spec.Build((bool b) => !b).Create("b");
        var specC = Spec.Build((bool b) => !b).Create("c");

        var spec = !(!specB | !specC);

        var act = spec.Evaluate(model);

        act.Satisfied.ShouldBe(expectedSatisfied);
        act.Reason.ShouldBe(expectedReason);
        act.Assertions.ShouldBe(expectedAssertions);
    }


    [Theory]
    [InlineData(true, true,"!((b == false) | (c == false))", "b == false", "c == false")]
    [InlineData(false,  false,"!((b == true) | (c == true))", "b == true", "c == true")]
    public void Should_negate_a_binary_operation_results_that_contains_double_negated_proposition_statements(
        bool model,
        bool expectedSatisfied,
        string expectedReason,
        params string[] expectedAssertions)
    {
        var specB = Spec.Build((bool b) => !b).Create("b");
        var specC = Spec.Build((bool b) => !b).Create("c");

        var spec = !(!!specB | !!specC);

        var act = spec.Evaluate(model);

        act.Satisfied.ShouldBe(expectedSatisfied);
        act.Reason.ShouldBe(expectedReason);
        act.Assertions.ShouldBe(expectedAssertions);
    }

    [Theory]
    [InlineData(true, false, "!(!b | !c)", "b", "c")]
    [InlineData(false, true, "!(!B | !C)", "B", "C")]
    public void Should_negate_a_binary_operation_results_that_contains_single_negated_proposition_statements_with_assertions(
        bool model,
        bool expectedSatisfied,
        string expectedReason,
        params string[] expectedAssertions)
    {
        var specB =
            Spec.Build((bool b) => !b)
                .WhenTrue("B")
                .WhenFalse("b")
                .Create("is b");
        var specC =
            Spec.Build((bool b) => !b)
                .WhenTrue("C")
                .WhenFalse("c")
                .Create("is c");

        var spec = !(!specB | !specC);

        var act = spec.Evaluate(model);

        act.Satisfied.ShouldBe(expectedSatisfied);
        act.Reason.ShouldBe(expectedReason);
        act.Assertions.ShouldBe(expectedAssertions);
    }

    [Theory]
    [InlineData(true, true, "!(b | c)", "b", "c")]
    [InlineData(false, false, "!(B | C)", "B", "C")]
    public void Should_negate_a_binary_operation_results_that_contains_double_negated_proposition_statements_with_assertions(
        bool model,
        bool expectedSatisfied,
        string expectedReason,
        params string[] expectedAssertions)
    {
        var specB =
            Spec.Build((bool b) => !b)
                .WhenTrue("B")
                .WhenFalse("b")
                .Create("is b");
        var specC =
            Spec.Build((bool b) => !b)
                .WhenTrue("C")
                .WhenFalse("c")
                .Create("is c");

        var spec = !(!!specB | !!specC);

        var act = spec.Evaluate(model);

        act.Satisfied.ShouldBe(expectedSatisfied);
        act.Reason.ShouldBe(expectedReason);
        act.Assertions.ShouldBe(expectedAssertions);
    }


}
