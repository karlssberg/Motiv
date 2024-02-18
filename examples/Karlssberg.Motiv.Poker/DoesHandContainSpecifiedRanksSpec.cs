using Humanizer;

namespace Karlssberg.Motiv.Poker;

public class DoesHandContainSpecifiedRanksSpec(ICollection<Rank> ranks) : Spec<Hand>(
    Spec.Build(UnderlyingSpec(ranks))
        .As(results => results.AllTrue())
        .WhenTrue($"all cards are either {ranks.Humanize("or")}")
        .WhenFalse(results => results.SelectMany(r => r.Explanation.Reasons))
        .CreateSpec()
        .ChangeModelTo<Hand>(hand => hand.Cards))
{
    private static SpecBase<Card, string> UnderlyingSpec(ICollection<Rank> ranks) =>
        Spec.Build<Card>(card => ranks.Contains(card.Rank))
            .WhenTrue($"Is one of {ranks.Humanize()}")
            .WhenFalse($"Is not one of {ranks.Humanize()}")
            .CreateSpec();
}