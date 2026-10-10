using Motiv.Explain;

#if !MOTIV_EXPLAIN
#error build/Motiv.Explain.targets should define MOTIV_EXPLAIN when MotivExplain is true
#endif

namespace Motiv.Explain.IntegrationTests;

public static partial class Rules
{
    [Explain]
    public static bool IsBigAndEven(int n) => n > 10 && n % 2 == 0;
}

public class ExplainWiringTests
{
    [Fact]
    public void An_explain_build_logs_each_direct_call()
    {
        var decisions = new List<ExplainedDecision>();
        MotivExplainLog.Sink = decisions.Add;
        try
        {
            Rules.IsBigAndEven(11).ShouldBeFalse();
        }
        finally
        {
            MotivExplainLog.Sink = null;
        }

        var decision = decisions.ShouldHaveSingleItem();
        decision.Member.ShouldBe("Motiv.Explain.IntegrationTests.Rules.IsBigAndEven(int)");
        decision.Satisfied.ShouldBeFalse();
        decision.Result.Assertions.ShouldBe(["n % 2 == 0 == false"]);
    }
}
