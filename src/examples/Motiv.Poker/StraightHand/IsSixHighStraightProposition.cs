using Motiv.Poker.Models;
using Motiv.Poker.Propositions;

namespace Motiv.Poker.StraightHand;

public class IsSixHighStraightProposition() : Spec<Hand>(
    Spec.Build(new DoAllCardsMatchRanksProposition(SixHighStraight))
        .WhenTrue("is Six High Straight")
        .WhenFalse("is Not Six High Straight")
        .Create())
{
    private static readonly ICollection<Rank> SixHighStraight =
        [Rank.Six, Rank.Five, Rank.Four, Rank.Three, Rank.Two];
}
