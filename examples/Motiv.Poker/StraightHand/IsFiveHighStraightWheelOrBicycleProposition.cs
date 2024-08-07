using Motiv.Poker.Models;
using Motiv.Poker.Propositions;

namespace Motiv.Poker.StraightHand;

public class IsFiveHighStraightWheelOrBicycleProposition() : Spec<Hand>(
    Spec.Build(new DoAllCardsMatchRanksProposition(FiveHighStraight))
        .WhenTrue("is Five High Straight Wheel Or Bicycle")
        .WhenFalse("is Not Five High Straight Wheel Or Bicycle")
        .Create())
{
    private static readonly ICollection<Rank> FiveHighStraight =
        [Rank.Five, Rank.Four, Rank.Three, Rank.Two, Rank.Ace];
}