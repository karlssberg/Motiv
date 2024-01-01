using Humanizer;

namespace Karlssberg.Motive.Poker;

public class DoesHandContainSpecifiedRanksSpec(ICollection<Rank> ranks) : Spec<Hand>(
    new Spec<Card>(
        card => ranks.Contains(card.Rank),
        $"Is one of {ranks.Humanize()}",
        $"Is not one of {ranks.Humanize()}")
    .ToAllSatisfiedSpec()
    .ChangeModel<Hand>(hand => hand.Cards));
         
