using Humanizer;

namespace Karlssberg.Motiv.Poker;

public class DoesHandContainSpecifiedRanksSpec(ICollection<Rank> ranks) : Spec<Hand>(
    () =>
    {
        var underlyingSpec = Spec
            .Build<Card>(card => ranks.Contains(card.Rank))
            .YieldWhenTrue($"Is one of {ranks.Humanize()}")
            .YieldWhenFalse($"Is not one of {ranks.Humanize()}")
            .CreateSpec();
            
        return underlyingSpec
            .ToAllSatisfiedSpec()
            .ChangeModel<Hand>(hand => hand.Cards);
    });  