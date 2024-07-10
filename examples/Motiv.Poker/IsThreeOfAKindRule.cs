using Motiv.Poker.Models;
using Motiv.Poker.Propositions;

namespace Motiv.Poker;

public class IsThreeOfAKindRule() : Policy<Hand, HandRank>(
    Spec.Build(new HasNCardsWithTheSameRankProposition(3))
        .WhenTrue(HandRank.ThreeOfAKind)
        .WhenFalse(HandRank.HighCard)
        .Create("is a three of a kind hand"));
