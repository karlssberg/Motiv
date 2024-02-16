using Humanizer;

namespace Karlssberg.Motiv.Poker;

public class DoesHandContainSpecifiedRanksSpec(ICollection<Rank> ranks) : Spec<Hand>(
    () =>
    {
        var underlyingSpec = Spec
            .Build<Card>(card => ranks.Contains(card.Rank))
            .WhenTrue($"Is one of {ranks.Humanize()}")
            .WhenFalse($"Is not one of {ranks.Humanize()}")
            .CreateSpec();

        return Spec
            .Build(underlyingSpec)
            .As(results => results.AllTrue())
            .WhenTrue($"all cards are either {ranks.Humanize("or")}")
            .WhenFalse(results => results.SelectMany(r => r.Reasons))
            .CreateSpec()
            .ChangeModelTo<Hand>(hand => hand.Cards);
    });