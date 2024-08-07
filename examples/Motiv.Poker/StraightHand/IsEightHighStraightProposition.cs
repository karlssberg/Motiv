using Motiv.Poker.Models;
using Motiv.Poker.Propositions;

namespace Motiv.Poker.StraightHand;

public class IsEightHighStraightProposition() : Spec<Hand>(
    Spec.Build(new DoAllCardsMatchRanksProposition(EightHighStraight))
        .WhenTrue("is Eight High Straight")
        .WhenFalse("is Not Eight High Straight")
        .Create())
{
    private static readonly ICollection<Rank> EightHighStraight =
        [Rank.Eight, Rank.Seven, Rank.Six, Rank.Five, Rank.Four];
}