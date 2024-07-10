using Motiv.Poker.Models;
using Motiv.Poker.Propositions;

namespace Motiv.Poker.StraightHand;

public class IsNineHighStraightProposition() : Spec<Hand>(
    Spec.Build(new DoAllCardsMatchRanksProposition(NineHighStraight))
        .WhenTrue("is Nine High Straight")
        .WhenFalse("is Not Nine High Straight")
        .Create())
{
    private static readonly ICollection<Rank> NineHighStraight =
        [Rank.Nine, Rank.Eight, Rank.Seven, Rank.Six, Rank.Five];
}
