namespace Motiv.Tests;

public class TutorialTests
{
    [Fact]
    public void Should_deomo_a_basic_spec()
    {
        var isEven = Spec
            .Build<int>(n => n % 2 == 0)
            .Create("is even");

        isEven.IsSatisfiedBy(2).Satisfied.Should().BeTrue();
        isEven.IsSatisfiedBy(2).Reason.Should().BeEquivalentTo("is even");
        isEven.IsSatisfiedBy(2).Assertions.Should().BeEquivalentTo("is even");

        isEven.IsSatisfiedBy(3).Satisfied.Should().BeFalse();
        isEven.IsSatisfiedBy(3).Reason.Should().BeEquivalentTo("!is even");
        isEven.IsSatisfiedBy(3).Assertions.Should().BeEquivalentTo("!is even");
    }

    [Fact]
    public void Should_deomo_a_basic_spec_using_strings_as_assertions()
    {
        var isEven = Spec
            .Build<int>(n => n % 2 == 0)
            .WhenTrue("number is even")
            .WhenFalse("number is odd")
            .Create();

        isEven.IsSatisfiedBy(2).Reason.Should().Be("number is even");
        isEven.IsSatisfiedBy(2).Assertions.Should().BeEquivalentTo("number is even");

        isEven.IsSatisfiedBy(3).Reason.Should().Be("number is odd");
        isEven.IsSatisfiedBy(3).Assertions.Should().BeEquivalentTo("number is odd");
    }

    [Fact]
    public void Should_demo_a_basic_spec_using_functions_as_assertion_functions()
    {
        var isEven = Spec
            .Build<int>(n => n % 2 == 0)
            .WhenTrue(n => $"{n} is even")
            .WhenFalse(n => $"{n} is odd")
            .Create("is even");

        isEven.IsSatisfiedBy(2).Reason.Should().Be("2 is even");
        isEven.IsSatisfiedBy(2).Assertions.Should().BeEquivalentTo("2 is even");

        isEven.IsSatisfiedBy(3).Reason.Should().Be("3 is odd");
        isEven.IsSatisfiedBy(3).Assertions.Should().BeEquivalentTo("3 is odd");
    }

    [Fact]
    public void Should_demo_handling_multiple_languages_spec()
    {
        var isEven = Spec
            .Build<int>(n => n % 2 == 0)
            .WhenTrue(_ => new { English = "the number is even", Spanish = "el número es par" })
            .WhenFalse(_ => new { English = "the number is odd", Spanish = "el número es impar" })
            .Create("is even number");

        isEven.IsSatisfiedBy(2).Satisfied.Should().BeTrue();
        isEven.IsSatisfiedBy(2).Reason.Should().Be("is even number");
        isEven.IsSatisfiedBy(2).Metadata.Select(m => m.English).Should().BeEquivalentTo("the number is even");
        isEven.IsSatisfiedBy(2).Metadata.Select(m => m.Spanish).Should().BeEquivalentTo("el número es par");
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

        isPositiveAndEven.IsSatisfiedBy(2).Satisfied.Should().BeTrue();
        isPositiveAndEven.IsSatisfiedBy(2).Reason.Should().Be("the number is positive & the number is even");
        isPositiveAndEven.IsSatisfiedBy(2).Assertions.Should().BeEquivalentTo("the number is positive", "the number is even");
        isPositiveAndEven.IsSatisfiedBy(2).AllAssertions.Should().BeEquivalentTo("the number is positive", "the number is even");

        isPositiveAndEven.IsSatisfiedBy(3).Satisfied.Should().BeFalse();
        isPositiveAndEven.IsSatisfiedBy(3).Reason.Should().Be("the number is odd");
        isPositiveAndEven.IsSatisfiedBy(3).Assertions.Should().BeEquivalentTo("the number is odd");
        isPositiveAndEven.IsSatisfiedBy(3).AllAssertions.Should().BeEquivalentTo("the number is positive", "the number is odd");

        isPositiveAndEven.IsSatisfiedBy(-2).Satisfied.Should().BeFalse();
        isPositiveAndEven.IsSatisfiedBy(-2).Reason.Should().Be("the number is negative");
        isPositiveAndEven.IsSatisfiedBy(-2).Assertions.Should().BeEquivalentTo("the number is negative");
        isPositiveAndEven.IsSatisfiedBy(-2).AllAssertions.Should().BeEquivalentTo("the number is negative", "the number is even");
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

        var act = allAreNegativeSpec.IsSatisfiedBy([-2, -1, 0, 1, 2]);

        act.Satisfied.Should().BeFalse();
        act.Assertions.Should().BeEquivalentTo("0  is not negative", "1  is not negative", "2  is not negative");
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

        isEvenAndPositiveSpec.IsSatisfiedBy(2).Satisfied.Should().BeTrue();
        isEvenAndPositiveSpec.IsSatisfiedBy(2).Reason.Should().Be("the number is even and positive");
        isEvenAndPositiveSpec.IsSatisfiedBy(-2).Reason.Should().Be("the number is not positive");
        isEvenAndPositiveSpec.IsSatisfiedBy(-3).Reason.Should().Be("the number is odd and not positive");
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

        var result = showShippingPageButton.IsSatisfiedBy(emptyBasket);

        result.Satisfied.Should().BeFalse();
        result.Reason.Should().Be("basket is empty");
    }

    private class IsNegativeIntegerProposition() : Spec<int>(
        Spec.Build((int n) => n < 0)
            .WhenTrue(n => $"{n} is negative")
            .WhenFalse(n => $"{n} is not negative")
            .Create("is negative"));

#if !NETFRAMEWORK

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

        allAreEven.IsSatisfiedBy([2, 4, 6, 8]).Satisfied.Should().BeTrue();
        allAreEven.IsSatisfiedBy([2, 4, 6, 8]).Assertions.Should().BeEquivalentTo("all are even");

        allAreEven.IsSatisfiedBy([10]).Satisfied.Should().BeTrue();
        allAreEven.IsSatisfiedBy([10]).Assertions.Should().BeEquivalentTo("10 is even and is the only item");


        allAreEven.IsSatisfiedBy([11]).Satisfied.Should().BeFalse();
        allAreEven.IsSatisfiedBy([11]).Assertions.Should().BeEquivalentTo("11 is odd and is the only item");

        allAreEven.IsSatisfiedBy([2, 4, 6, 9]).Satisfied.Should().BeFalse();
        allAreEven.IsSatisfiedBy([2, 4, 6, 9]).Assertions.Should().BeEquivalentTo("only 9 is odd");

        allAreEven.IsSatisfiedBy([]).Satisfied.Should().BeTrue();
        allAreEven.IsSatisfiedBy([]).Assertions.Should().BeEquivalentTo("the collection is empty");

        allAreEven.IsSatisfiedBy([1, 3, 5, 7]).Satisfied.Should().BeFalse();
        allAreEven.IsSatisfiedBy([1, 3, 5, 7]).Assertions.Should().BeEquivalentTo("all are odd");

        allAreEven.IsSatisfiedBy([2, 4, 5, 7]).Satisfied.Should().BeFalse();
        allAreEven.IsSatisfiedBy([2, 4, 5, 7]).Assertions.Should().BeEquivalentTo("5 is odd", "7 is odd");
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


        allAreNegative.IsSatisfiedBy([]).Satisfied.Should().BeTrue();
        allAreNegative.IsSatisfiedBy([]).Assertions.Should().BeEquivalentTo("there is an absence of numbers");

        allAreNegative.IsSatisfiedBy([-10]).Satisfied.Should().BeTrue();
        allAreNegative.IsSatisfiedBy([-10]).Assertions.Should()
            .BeEquivalentTo("-10 is negative and is the only number");

        allAreNegative.IsSatisfiedBy([-2, -4, -6, -8]).Satisfied.Should().BeTrue();
        allAreNegative.IsSatisfiedBy([-2, -4, -6, -8]).Assertions.Should().BeEquivalentTo("all are negative numbers");

        allAreNegative.IsSatisfiedBy([0]).Satisfied.Should().BeFalse();
        allAreNegative.IsSatisfiedBy([0]).Assertions.Should()
            .BeEquivalentTo("the number is 0 and is the only number");

        allAreNegative.IsSatisfiedBy([11]).Satisfied.Should().BeFalse();
        allAreNegative.IsSatisfiedBy([11]).Assertions.Should()
            .BeEquivalentTo("11 is positive and is the only number");

        allAreNegative.IsSatisfiedBy([0, 0, 0, 0]).Satisfied.Should().BeFalse();
        allAreNegative.IsSatisfiedBy([0, 0, 0, 0]).Assertions.Should().BeEquivalentTo("all are 0");

        allAreNegative.IsSatisfiedBy([2, 4, 6, 8]).Satisfied.Should().BeFalse();
        allAreNegative.IsSatisfiedBy([2, 4, 6, 8]).Assertions.Should().BeEquivalentTo("all are positive numbers");

        allAreNegative.IsSatisfiedBy([0, 1, 2, 3]).Satisfied.Should().BeFalse();
        allAreNegative.IsSatisfiedBy([0, 1, 2, 3]).Assertions.Should().BeEquivalentTo("none are negative numbers");


        allAreNegative.IsSatisfiedBy([-2, -4, 0, 9]).Satisfied.Should().BeFalse();
        allAreNegative.IsSatisfiedBy([-2, -4, 0, 9]).Assertions.Should().BeEquivalentTo("0 is not negative", "9 is not negative");
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
            Spec.Build((int n) => isLongEvenSpec.IsSatisfiedBy(n) & isDecimalPositiveSpec.IsSatisfiedBy(n))
                .Create("even and positive");

        isIntegerPositiveAndEvenSpec.IsSatisfiedBy(2).AllRootAssertions.Should().BeEquivalentTo("even", "positive");
        isIntegerPositiveAndEvenSpec.IsSatisfiedBy(3).AllRootAssertions.Should().BeEquivalentTo("odd", "positive");
        isIntegerPositiveAndEvenSpec.IsSatisfiedBy(0).AllRootAssertions.Should().BeEquivalentTo("even", "not positive");
        isIntegerPositiveAndEvenSpec.IsSatisfiedBy(-3).AllRootAssertions.Should().BeEquivalentTo("odd", "not positive");
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


        var canCheckIn = canCheckInSpec.IsSatisfiedBy(validPassenger);

        canCheckIn.Satisfied.Should().Be(true);
        canCheckIn.Reason.Should().BeEquivalentTo("has a valid ticket & does not have outstanding fees & check-in is open");
        canCheckIn.Assertions.Should().BeEquivalentTo("has a valid ticket", "does not have outstanding fees", "check-in is open");
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
            Spec.Build((Customer customer) => customer.CreditScore > 600)
                .WhenTrue("customer has a good credit score")
                .WhenFalse("customer has an inadequate credit score")
                .Create();

        var hasSufficientIncome =
            Spec.Build((Customer customer) => customer.Income > 100000)
                .WhenTrue("customer has sufficient income")
                .WhenFalse("customer has insufficient income")
                .Create();

        var isEligibleForLoan =
            Spec.Build(hasGoodCreditScore & hasSufficientIncome)
                .WhenTrue("customer is eligible for a loan")
                .WhenFalseYield((_, result) => result.Assertions) // reusing assertions from the original propositions
                .Create();

        var act = isEligibleForLoan.IsSatisfiedBy(new Customer(200, 20000));

        act.Satisfied.Should().BeFalse();

        act.Justification.Should().Be(
            """
            !customer is eligible for a loan
                AND
                    customer has an inadequate credit score
                    customer has insufficient income
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

        var act = hasSubscriptionInGracePeriod.IsSatisfiedBy(new Subscription(DateTime.Now.AddDays(-3)));

            act.Satisfied.Should().BeTrue();
            act.Reason.Should().Be("subscription is within grace period");
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

        var act = areEven.IsSatisfiedBy([ 1, 2, 3, 4 ]);

        act.GetRootAssertions().Should().BeEquivalentTo(["is even"]);
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

        var act = areEven.IsSatisfiedBy([ 1, 2, 3, 4 ]);

        act.GetAllRootAssertions().Should().BeEquivalentTo(["is even", "is odd"]);
    }

    [Theory]
    [InlineData(1, false, "1")]
    [InlineData(2, false, "2")]
    [InlineData(3, true, "fizz")]
    [InlineData(4, false, "4")]
    [InlineData(5, true, "buzz")]
    [InlineData(6, true, "fizz")]
    [InlineData(7, false, "7")]
    [InlineData(8, false, "8")]
    [InlineData(9, true, "fizz")]
    [InlineData(10, true, "buzz")]
    [InlineData(11, false, "11")]
    [InlineData(12, true, "fizz")]
    [InlineData(13, false, "13")]
    [InlineData(14, false, "14")]
    [InlineData(15, true, "fizzbuzz")]
    [InlineData(16, false, "16")]
    [InlineData(17, false, "17")]
    [InlineData(18, true, "fizz")]
    [InlineData(19, false, "19")]
    [InlineData(20, true, "buzz")]
    public void Should_solve_fizzbuzz(int number, bool expectedSatisfied, string expectedReason)
    {
        var isFizz =
            Spec.Build((int n) => n % 3 == 0)
                .Create("fizz");

        var isBuzz =
            Spec.Build((int n) => n % 5 == 0)
                .Create("buzz");

        var isSubstitution =
            Spec.Build(isFizz | isBuzz)
                .WhenTrue((_, result) => string.Concat(result.Assertions))
                .WhenFalse(n => n.ToString())
                .Create("should substitute number");

        var act = isSubstitution.IsSatisfiedBy(number);

        act.Satisfied.Should().Be(expectedSatisfied);
        act.Reason.Should().Be(expectedReason);
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
        var result = isPartiallyFull.IsSatisfiedBy(5);

        result.Satisfied.Should().BeTrue();
        result.Assertions.Should().BeEquivalentTo(["valid", "!empty", "!full"]);
        result.Justification.Should().Be(
            """
            AND
                valid
                NOR
                    !empty
                    !full
            """);
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

        spec.IsSatisfiedBy(true).Assertions.Should().BeEquivalentTo("left");
        spec.IsSatisfiedBy(false).Assertions.Should().BeEquivalentTo("right");
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

        spec.IsSatisfiedBy(true).Assertions.Should().BeEquivalentTo("!right");
        spec.IsSatisfiedBy(false).Assertions.Should().BeEquivalentTo("!left");
    }
}
