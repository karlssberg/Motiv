namespace Motiv.Tests;

public class TutorialTests
{
    [Fact]
    public void Should_deomo_a_basic_spec()
    {
        var isEven = Spec
            .Build<int>(n => n % 2 == 0)
            .Create("is even");

        isEven.Evaluate(2).Satisfied.ShouldBeTrue();
        isEven.Evaluate(2).Reason.ShouldBe("is even == true");
        isEven.Evaluate(2).Assertions.ShouldBe(["is even == true"]);

        isEven.Evaluate(3).Satisfied.ShouldBeFalse();
        isEven.Evaluate(3).Reason.ShouldBe("is even == false");
        isEven.Evaluate(3).Assertions.ShouldBe(["is even == false"]);
    }

    [Fact]
    public void Should_demo_a_basic_spec_using_strings_as_assertions()
    {
        var isEven = Spec
            .Build<int>(n => n % 2 == 0)
            .WhenTrue("number is even")
            .WhenFalse("number is odd")
            .Create();

        isEven.Evaluate(2).Reason.ShouldBe("number is even");
        isEven.Evaluate(2).Assertions.ShouldBe(["number is even"]);

        isEven.Evaluate(3).Reason.ShouldBe("number is odd");
        isEven.Evaluate(3).Assertions.ShouldBe(["number is odd"]);
    }

    [Fact]
    public void Should_demo_a_basic_spec_using_functions_as_assertion_functions()
    {
        var isEven = Spec
            .Build<int>(n => n % 2 == 0)
            .WhenTrue(n => $"{n} is even")
            .WhenFalse(n => $"{n} is odd")
            .Create("is even");

        isEven.Evaluate(2).Reason.ShouldBe("is even == true");
        isEven.Evaluate(2).Assertions.ShouldBe(["is even == true"]);

        isEven.Evaluate(3).Reason.ShouldBe("is even == false");
        isEven.Evaluate(3).Assertions.ShouldBe(["is even == false"]);
    }

    [Fact]
    public void Should_demo_handling_multiple_languages_spec()
    {
        var isEven = Spec
            .Build<int>(n => n % 2 == 0)
            .WhenTrue(_ => new { English = "the number is even", Spanish = "el número es par" })
            .WhenFalse(_ => new { English = "the number is odd", Spanish = "el número es impar" })
            .Create("is even number");

        isEven.Evaluate(2).Satisfied.ShouldBeTrue();
        isEven.Evaluate(2).Reason.ShouldBe("is even number == true");
        isEven.Evaluate(2).Values.Select(m => m.English).ShouldBe(["the number is even"]);
        isEven.Evaluate(2).Values.Select(m => m.Spanish).ShouldBe(["el número es par"]);
    }

    [Fact]
    public void Should_demo_spec_decorator()
    {
        var isPositive = Spec
            .Build<int>(n => n > 0)
            .WhenTrue("the number is positive")
            .WhenFalse(n => $"the number is {(n < 0 ? "negative" : "zero")}")
            .Create();

        var isEven = Spec
            .Build<int>(n => n % 2 == 0)
            .WhenTrue("the number is even")
            .WhenFalse("the number is odd")
            .Create();

        var isPositiveAndEven = isPositive & isEven;

        isPositiveAndEven.Evaluate(2).Satisfied.ShouldBeTrue();
        isPositiveAndEven.Evaluate(2).Reason.ShouldBe("the number is positive & the number is even");
        isPositiveAndEven.Evaluate(2).Assertions.ShouldBe(["the number is positive", "the number is even"]);
        isPositiveAndEven.Evaluate(2).AllAssertions.ShouldBe(["the number is positive", "the number is even"]);

        isPositiveAndEven.Evaluate(3).Satisfied.ShouldBeFalse();
        isPositiveAndEven.Evaluate(3).Reason.ShouldBe("the number is odd");
        isPositiveAndEven.Evaluate(3).Assertions.ShouldBe(["the number is odd"]);
        isPositiveAndEven.Evaluate(3).AllAssertions.ShouldBe(["the number is positive", "the number is odd"]);

        isPositiveAndEven.Evaluate(-2).Satisfied.ShouldBeFalse();
        isPositiveAndEven.Evaluate(-2).Reason.ShouldBe("the number is negative");
        isPositiveAndEven.Evaluate(-2).Assertions.ShouldBe(["the number is negative"]);
        isPositiveAndEven.Evaluate(-2).AllAssertions.ShouldBe(["the number is negative", "the number is even"]);
    }

    [Fact]
    public void Should_demonstrate_higher_order_factory_methods()
    {
        var isNegative =
            Spec.Build((int n) => n < 0)
                .WhenTrue("the number is negative")
                .WhenFalse("the number is not negative")
                .Create();

        var allAreNegativeSpec =
            Spec.Build(isNegative)
                .AsAllSatisfied()
                .WhenTrue("all are negative")
                .WhenFalseYield(evaluation => evaluation switch
                {
                    { FalseCount: <= 10 } => evaluation.FalseModels.Select(n => $"{n}  is not negative"),
                    _ => [$"{evaluation.FalseCount} of {evaluation.Count} are not negative"]
                })
                .Create();

        var act = allAreNegativeSpec.Evaluate([-2, -1, 0, 1, 2]);

        act.Satisfied.ShouldBeFalse();
        act.Assertions.ShouldBe(["0  is not negative", "1  is not negative", "2  is not negative"]);
    }

    [Fact]
    public void Should_demonstrate_composition_using_spec_type()
    {
        var isEvenSpec =
            Spec.Build((int n) => n % 2 == 0)
                .WhenTrue("even")
                .WhenFalse("odd")
                .Create();

        var isPositiveSpec =
            Spec.Build((int n) => n > 0)
                .WhenTrue("positive")
                .WhenFalse("not positive")
                .Create();

        var isEvenAndPositiveSpec =
            Spec.Build(isEvenSpec & isPositiveSpec)
                .WhenTrue("the number is even and positive")
                .WhenFalse((_,evaluation) => $"the number is {evaluation.Assertions.Serialize()}")
                .Create();

        isEvenAndPositiveSpec.Evaluate(2).Satisfied.ShouldBeTrue();
        isEvenAndPositiveSpec.Evaluate(2).Reason.ShouldBe("the number is even and positive");
        isEvenAndPositiveSpec.Evaluate(-2).Reason.ShouldBe("the number is not positive");
        isEvenAndPositiveSpec.Evaluate(-3).Reason.ShouldBe("the number is odd and not positive");
    }

    public record BasketItem(bool FreeShipping)
    {
        public bool FreeShipping { get; } = FreeShipping;
    }

    public record Basket(ICollection<BasketItem> Items)
    {
        public ICollection<BasketItem> Items { get; } = Items;
    }

    [Fact]
    public void Should_demonstrate_and_also_operation()
    {
        var emptyBasket = new Basket(Array.Empty<BasketItem>());
        var isBasketEmptySpec =
            Spec.Build((Basket b) => b.Items.Count == 0)
                .WhenTrue("basket is empty")
                .WhenFalse(o => $"basket contains {o.Items.Count} items")
                .Create();

        var isFreeShippingSpec =
            Spec.Build((Basket b) => b.Items.All(i => i.FreeShipping))
                .WhenTrue("free shipping")
                .WhenFalse("shipping payment required")
                .Create();

        var showShippingPageButton = (!isBasketEmptySpec).AndAlso(!isFreeShippingSpec);

        var result = showShippingPageButton.Evaluate(emptyBasket);

        result.Satisfied.ShouldBeFalse();
        result.Reason.ShouldBe("!basket is empty");
    }

#if !NETFRAMEWORK && !NETSTANDARD2_0
    private class IsNegativeIntegerProposition() : Spec<int>(
        Spec.Build((int n) => n < 0)
            .WhenTrue(n => $"{n} is negative")
            .WhenFalse(n => $"{n} is not negative")
            .Create("is negative"));

    [Fact]
    public void Should_demonstrate_is_even_spec_as_an_all_satisfied_higher_order_logic()
    {
        var isEven =
            Spec.Build<int>(n => n % 2 == 0)
                .WhenTrue("even")
                .WhenFalse("odd")
                .Create();

        var allAreEven =
            Spec.Build(isEven)
                .AsAllSatisfied()
                .WhenTrue(evaluation =>
                    evaluation switch
                    {
                        { Count: 0 } => "the collection is empty",
                        { Models: [var n] } => $"{n} is even and is the only item",
                        _ => "all are even"
                    })
                .WhenFalseYield(evaluation =>
                    evaluation switch
                    {
                        { Models: [var n] } => [$"{n} is odd and is the only item"],
                        { FalseModels: [var n] } => [$"only {n} is odd"],
                        { NoneSatisfied: true } => ["all are odd"],
                        _ => evaluation.FalseModels.Select(n => $"{n} is odd")
                    })
                .Create("all are even");

        allAreEven.Evaluate([2, 4, 6, 8]).Satisfied.ShouldBeTrue();
        allAreEven.Evaluate([2, 4, 6, 8]).Assertions.ShouldBe(["all are even"]);

        allAreEven.Evaluate([10]).Satisfied.ShouldBeTrue();
        allAreEven.Evaluate([10]).Assertions.ShouldBe(["10 is even and is the only item"]);


        allAreEven.Evaluate([11]).Satisfied.ShouldBeFalse();
        allAreEven.Evaluate([11]).Assertions.ShouldBe(["11 is odd and is the only item"]);

        allAreEven.Evaluate([2, 4, 6, 9]).Satisfied.ShouldBeFalse();
        allAreEven.Evaluate([2, 4, 6, 9]).Assertions.ShouldBe(["only 9 is odd"]);

        allAreEven.Evaluate([]).Satisfied.ShouldBeTrue();
        allAreEven.Evaluate([]).Assertions.ShouldBe(["the collection is empty"]);

        allAreEven.Evaluate([1, 3, 5, 7]).Satisfied.ShouldBeFalse();
        allAreEven.Evaluate([1, 3, 5, 7]).Assertions.ShouldBe(["all are odd"]);

        allAreEven.Evaluate([2, 4, 5, 7]).Satisfied.ShouldBeFalse();
        allAreEven.Evaluate([2, 4, 5, 7]).Assertions.ShouldBe(["5 is odd", "7 is odd"]);
    }

    [Fact]
    public void Should_demonstrate_is_negative_spec_as_an_all_satisfied_higher_order_logic()
    {

        var allAreNegative =
            Spec.Build(new IsNegativeIntegerProposition())
                .AsAllSatisfied()
                .WhenTrue(eval =>
                    eval switch
                    {
                        { Count: 0 } => "there is an absence of numbers",
                        { Models: [< 0 and var n] } => $"{n} is negative and is the only number",
                        _ => "all are negative numbers"
                    })
                .WhenFalseYield(eval =>
                    eval switch
                    {
                        { Models: [0] } => ["the number is 0 and is the only number"],
                        { Models: [> 0 and var n] } => [$"{n} is positive and is the only number"],
                        { NoneSatisfied: true } when eval.Models.All(m => m is 0) => ["all are 0"],
                        { NoneSatisfied: true } when eval.Models.All(m => m > 0) => ["all are positive numbers"],
                        { NoneSatisfied: true } =>  ["none are negative numbers"],
                        _ => eval.FalseResults.GetAssertions()
                    })
                .Create("all are negative");


        allAreNegative.Evaluate([]).Satisfied.ShouldBeTrue();
        allAreNegative.Evaluate([]).Assertions.ShouldBe(["there is an absence of numbers"]);

        allAreNegative.Evaluate([-10]).Satisfied.ShouldBeTrue();
        allAreNegative.Evaluate([-10]).Assertions.ShouldBe(["-10 is negative and is the only number"]);

        allAreNegative.Evaluate([-2, -4, -6, -8]).Satisfied.ShouldBeTrue();
        allAreNegative.Evaluate([-2, -4, -6, -8]).Assertions.ShouldBe(["all are negative numbers"]);

        allAreNegative.Evaluate([0]).Satisfied.ShouldBeFalse();
        allAreNegative.Evaluate([0]).Assertions.ShouldBe(["the number is 0 and is the only number"]);

        allAreNegative.Evaluate([11]).Satisfied.ShouldBeFalse();
        allAreNegative.Evaluate([11]).Assertions.ShouldBe(["11 is positive and is the only number"]);

        allAreNegative.Evaluate([0, 0, 0, 0]).Satisfied.ShouldBeFalse();
        allAreNegative.Evaluate([0, 0, 0, 0]).Assertions.ShouldBe(["all are 0"]);

        allAreNegative.Evaluate([2, 4, 6, 8]).Satisfied.ShouldBeFalse();
        allAreNegative.Evaluate([2, 4, 6, 8]).Assertions.ShouldBe(["all are positive numbers"]);

        allAreNegative.Evaluate([0, 1, 2, 3]).Satisfied.ShouldBeFalse();
        allAreNegative.Evaluate([0, 1, 2, 3]).Assertions.ShouldBe(["none are negative numbers"]);


        allAreNegative.Evaluate([-2, -4, 0, 9]).Satisfied.ShouldBeFalse();
        allAreNegative.Evaluate([-2, -4, 0, 9]).Assertions.ShouldBe(["is negative == false"]);
    }

#endif

    [Fact]
    public void Should_harvest_assertions_from_a_boolean_result_predicate()
    {
        var isLongEvenSpec =
            Spec.Build((long n) => n % 2 == 0)
                .WhenTrue("even")
                .WhenFalse("odd")
                .Create();

        var isDecimalPositiveSpec =
            Spec.Build((decimal n) => n > 0)
                .WhenTrue("positive")
                .WhenFalse("not positive")
                .Create();

        var isIntegerPositiveAndEvenSpec =
            Spec.Build((int n) => isLongEvenSpec.Evaluate(n) & isDecimalPositiveSpec.Evaluate(n))
                .Create("even and positive");

        isIntegerPositiveAndEvenSpec.Evaluate(2).AllRootAssertions.ShouldBe(["even", "positive"]);
        isIntegerPositiveAndEvenSpec.Evaluate(3).AllRootAssertions.ShouldBe(["odd", "positive"]);
        isIntegerPositiveAndEvenSpec.Evaluate(0).AllRootAssertions.ShouldBe(["even", "not positive"]);
        isIntegerPositiveAndEvenSpec.Evaluate(-3).AllRootAssertions.ShouldBe(["odd", "not positive"]);
    }

    private record Passenger(bool HasValidTicket, decimal OutstandingFees, DateTime FlightTime)
    {
        public bool HasValidTicket { get; } = HasValidTicket;
        public decimal OutstandingFees { get; } = OutstandingFees;
        public DateTime FlightTime { get; } = FlightTime;
    }

    [Fact]
    public void Can_check_in_a_flight_demo()
    {
        var hasValidTicketSpec =
            Spec.Build((Passenger passenger) => passenger.HasValidTicket)
                .WhenTrue("has a valid ticket")
                .WhenFalse("does not have a valid ticket")
                .Create();

        var hasOutstandingFeesSpec =
            Spec.Build((Passenger passenger) => passenger.OutstandingFees > 0)
                .WhenTrue("has outstanding fees")
                .WhenFalse("does not have outstanding fees")
                .Create();

        var isCheckInOpenSpec =
            Spec.Build((Passenger passenger) =>
                        DateTime.Now is var now
                        && passenger.FlightTime - now <= TimeSpan.FromHours(4)
                        && passenger.FlightTime - now >= TimeSpan.FromMinutes(30))
                    .WhenTrue("check-in is open")
                    .WhenFalse("check-in is closed")
                    .Create();

        var canCheckInSpec = hasValidTicketSpec & !hasOutstandingFeesSpec & isCheckInOpenSpec;

        var validPassenger = new Passenger(true, 0, DateTime.Now.AddHours(1));


        var canCheckIn = canCheckInSpec.Evaluate(validPassenger);

        canCheckIn.Satisfied.ShouldBe(true);
        canCheckIn.Reason.ShouldBe("has a valid ticket & !does not have outstanding fees & check-in is open");
        canCheckIn.Assertions.ShouldBe(["has a valid ticket", "does not have outstanding fees", "check-in is open"]);
    }

    public record Customer(int CreditScore, decimal Income)
    {
        public int CreditScore { get; } = CreditScore;
        public decimal Income { get; } = Income;
    }

    [Fact]
    public void Should_be_eligible_for_a_loan()
    {
        var hasGoodCreditScore =
            Spec.From((Customer customer) => customer.CreditScore > 600)
                .WhenTrue("customer has a good credit score")
                .WhenFalse("customer has an inadequate credit score")
                .Create();

        var hasSufficientIncome =
            Spec.From((Customer customer) => customer.Income > 100000)
                .WhenTrue("customer has sufficient income")
                .WhenFalse("customer has insufficient income")
                .Create();

        var isEligibleForLoan =
            Spec.Build(hasGoodCreditScore & hasSufficientIncome)
                .WhenTrue("customer is eligible for a loan")
                .WhenFalseYield((_, result) => result.Assertions) // reusing assertions from the original propositions
                .Create();

        var act = isEligibleForLoan.Evaluate(new Customer(200, 20000));

        act.Satisfied.ShouldBeFalse();

        act.Justification.ShouldBe(
            """
            customer is eligible for a loan == false
                AND
                    customer has an inadequate credit score
                        (Customer customer) => customer.CreditScore > 600 == false
                            customer.CreditScore > 600 == false
                    customer has insufficient income
                        (Customer customer) => customer.Income > 100000 == false
                            customer.Income > 100000 == false
            """);
    }

    private record Subscription(DateTime ExpiryDate)
    {
        public DateTime ExpiryDate { get; } = ExpiryDate;
    }

    [Fact]
    public void Should_have_subscription_in_grace_period()
    {
        var hasSubscriptionInGracePeriod =
        Spec.Build((Subscription subscription) =>
                DateTime.Now is var now
                && subscription.ExpiryDate < now
                && now < subscription.ExpiryDate.AddDays(7))
            .WhenTrue("subscription is within grace period")
            .WhenFalse("subscription is not within grace period")
            .Create();

        var act = hasSubscriptionInGracePeriod.Evaluate(new Subscription(DateTime.Now.AddDays(-3)));

            act.Satisfied.ShouldBeTrue();
            act.Reason.ShouldBe("subscription is within grace period");
    }

    [Fact]
    public void Should_get_root_assertions()
    {
        var isEven =
            Spec.Build<int>(n => n % 2 == 0)
                .WhenTrue("is even")
                .WhenFalse("is odd")
                .Create();

        var areEven =
            Spec.Build(isEven)
                .AsAnySatisfied()
                .WhenTrue("some even")
                .WhenFalse("all odd")
                .Create();

        var act = areEven.Evaluate([ 1, 2, 3, 4 ]);

        act.GetRootAssertions().ShouldBe(["is even"]);
    }


    [Fact]
    public void Should_get_all_root_assertions()
    {
        var isEven =
            Spec.Build<int>(n => n % 2 == 0)
                .WhenTrue("is even")
                .WhenFalse("is odd")
                .Create();

        var areEven =
            Spec.Build(isEven)
                .AsAnySatisfied()
                .WhenTrue("some even")
                .WhenFalse("all odd")
                .Create();

        var act = areEven.Evaluate([ 1, 2, 3, 4 ]);

        act.GetAllRootAssertions().ShouldBe(["is even", "is odd"], true);
    }

    [Theory]
    [InlineData(1, false, "1")]
    [InlineData(2, false, "2")]
    [InlineData(3, true, "fizz == true")]
    [InlineData(4, false, "4")]
    [InlineData(5, true, "buzz == true")]
    [InlineData(6, true, "fizz == true")]
    [InlineData(7, false, "7")]
    [InlineData(8, false, "8")]
    [InlineData(9, true, "fizz == true")]
    [InlineData(10, true, "buzz == true")]
    [InlineData(11, false, "11")]
    [InlineData(12, true, "fizz == true")]
    [InlineData(13, false, "13")]
    [InlineData(14, false, "14")]
    [InlineData(15, true, "fizz == truebuzz == true")]
    [InlineData(16, false, "16")]
    [InlineData(17, false, "17")]
    [InlineData(18, true, "fizz == true")]
    [InlineData(19, false, "19")]
    [InlineData(20, true, "buzz == true")]
    public void Should_solve_fizzbuzz(int number, bool expectedSatisfied, string expectedReason)
    {
        var isFizz =
            Spec.Build((int n) => n % 3 == 0)
                .WhenTrue("fizz")
                .WhenFalse("")
                .Create("fizz");

        var isBuzz =
            Spec.Build((int n) => n % 5 == 0)
                .WhenTrue("buzz")
                .WhenFalse("")
                .Create("buzz");

        var isSubstitution =
            Spec.Build(isFizz | isBuzz)
                .WhenTrue((_, result) => string.Concat(result.Assertions))
                .WhenFalse(n => n.ToString())
                .Create("should substitute number");

        var act = isSubstitution.Evaluate(number);

        act.Satisfied.ShouldBe(expectedSatisfied);
        act.Value.ShouldBe(expectedReason);
    }

    [Fact]
    public void Should_evaluate_initial_example_in_docs()
    {
        // Define clauses
        var isValid = Spec.Build((int n) => n is >= 0 and <= 11).Create("valid");
        var isEmpty = Spec.Build((int n) => n == 0).Create("empty");
        var isFull = Spec.Build((int n) => n == 11).Create("full");

        // Compose new proposition
        var isPartiallyFull = isValid & !(isEmpty | isFull);

        // Evaluate proposition
        var result = isPartiallyFull.Evaluate(5);

        result.Satisfied.ShouldBeTrue();
        result.Assertions.ShouldBe(["valid == true", "empty == false", "full == false"]);
        result.Reason.ShouldBe("(valid == true) & !((empty == false) | (full == false))");
        result.Justification.ShouldBe(
            """
            AND
                valid == true
                NOR
                    empty == false
                    full == false
            """);
    }

    [Fact]
    public void Should_evaluate_initial_example_in_readme()
    {
        // Define the proposition
        var isInRangeAndEven = Spec.From((int n) => n >= 1 & n <= 10 & n % 2 == 0)
            .Create("in range and even");

        // Evaluate proposition (elsewhere in your code)
        var result = isInRangeAndEven.Evaluate(11);

        result.Satisfied.ShouldBeFalse();
        result.Reason.ShouldBe("in range and even == false");
        result.Assertions.ShouldBe(["n > 10", "n % 2 != 0"], true);
    }

    [Fact]
    public void Should_demonstrate_the_usage_of_GetTrueAssertions()
    {
        var left = Spec.Build((bool b) => b).Create("left");
        var right = Spec.Build((bool b) => !b).Create("right");

        var spec =
            Spec .Build(left ^ right)
                .WhenTrueYield((_, result) => result.Causes.GetTrueAssertions())
                .WhenFalse("none")
                .Create("xor");

        spec.Evaluate(true).Assertions.ShouldBe(["left == true"]);
        spec.Evaluate(false).Assertions.ShouldBe(["right == true"]);
    }

    [Fact]
    public void Should_demonstrate_the_usage_of_GetFalseAssertions()
    {
        var left = Spec.Build((bool b) => b).Create("left");
        var right = Spec.Build((bool b) => !b).Create("right");

        var spec =
            Spec .Build(left ^ right)
                .WhenTrueYield((_, result) => result.Causes.GetFalseAssertions())
                .WhenFalse("none")
                .Create("xor");

        spec.Evaluate(true).Assertions.ShouldBe(["right == false"]);
        spec.Evaluate(false).Assertions.ShouldBe(["left == false"]);
    }

    [Fact]
    public void Should_demonstrate_higher_order_operation_in_readme()
    {
        SpecBase<IEnumerable<int>, string> allNegative =
            Spec.Build((int n) => n < 0)
                .AsAllSatisfied()
                .WhenTrue("all are negative")
                .WhenFalseYield(eval => eval.FalseModels.Select(n => $"{n} is not negative"))
                .Create();

        BooleanResultBase<string> result = allNegative.Evaluate([-1, 2, 3]);

        result.Satisfied.ShouldBeFalse();
        result.Reason.ShouldBe("all are negative == false");
        result.Assertions.ShouldBe(["2 is not negative", "3 is not negative"]);
    }

    [Fact]
    public void Should_demonstrate_the_usage_of_all_and_any()
    {
        var areAnyEvenAndAllPositive = Spec.From((ICollection<int> numbers) =>
                numbers.Any(n => n % 2 == 0)
                & numbers.All(n => n > 0))
            .Create("all positive numbers amd some are even");

        var result = areAnyEvenAndAllPositive.Evaluate([-1, 2, 3]);

        result.Satisfied.ShouldBeFalse();
        result.Assertions.ShouldBe(["n <= 0"]);
        result.Justification.ShouldBe(
            """
            all positive numbers amd some are even == false
                (ICollection<int> numbers) => (numbers.Any((int n) => n % 2 == 0) & numbers.All((int n) => n > 0)) == false
                    AND
                        numbers.All((int n) => n > 0) == false
                            (int n) => n > 0 == false
                                n > 0 == false
            """);
    }
}
