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

        return underlyingSpec
            .CreateAllSpec($"all cards are either {ranks.Humanize("or")}")
            .ChangeModel<Hand>(hand => hand.Cards);
    });