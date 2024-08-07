using Motiv.Poker.Models;
using Motiv.Poker.Propositions;

namespace Motiv.Poker.StraightHand;

public class IsAceHighStraightBroadwayProposition() : Spec<Hand>(
    Spec.Build(new DoAllCardsMatchRanksProposition(BroadwayRanks))
        .WhenTrue("is Ace High Straight Broadway")
        .WhenFalse("is Not Ace High Straight Broadway")
        .Create())
{
    private static readonly ICollection<Rank> BroadwayRanks =
        [Rank.Ace, Rank.King, Rank.Queen, Rank.Jack, Rank.Ten];
}
