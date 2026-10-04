namespace Motiv.Tests;

// Pins the agent example shown in README.md ("Reasons an agent can act on") and docs/Overview.md
// ("Why Motiv"). If an expectation here changes, update both documents in the same commit.
public class AgentToolExampleTests
{
    private record RefundRequest(decimal Amount, int DaysSincePurchase, bool IsFinalSale);

    private static readonly SpecBase<RefundRequest, string> CanRefund = Spec
        .From((RefundRequest request) =>
            request.Amount <= 500 &
            request.DaysSincePurchase <= 30 &
            !request.IsFinalSale)
        .Create("refund allowed");

    private static string IssueRefund(RefundRequest request)
    {
        var result = CanRefund.Evaluate(request);
        if (!result.Satisfied)
            return $"Refused: {string.Join("; ", result.Assertions)}";

        return "Refund issued";
    }

    [Fact]
    public void Should_tell_the_agent_only_the_clause_that_refused_the_tool_call()
    {
        IssueRefund(new RefundRequest(Amount: 750, DaysSincePurchase: 12, IsFinalSale: false))
            .ShouldBe("Refused: request.Amount > 500");
    }

    [Fact]
    public void Should_issue_the_refund_once_the_agent_corrects_the_amount()
    {
        IssueRefund(new RefundRequest(Amount: 500, DaysSincePurchase: 12, IsFinalSale: false))
            .ShouldBe("Refund issued");
    }

    [Fact]
    public void Should_report_the_named_reason_and_the_failing_clause_as_shown_in_the_overview()
    {
        var result = CanRefund.Evaluate(new RefundRequest(Amount: 750, DaysSincePurchase: 12, IsFinalSale: false));

        result.Satisfied.ShouldBeFalse();
        result.Reason.ShouldBe("refund allowed == false");
        result.Assertions.ShouldBe(["request.Amount > 500"]);
    }
}
