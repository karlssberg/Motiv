using Motiv.Poker.Models;
using Motiv.Poker.Propositions;

namespace Motiv.Poker;

public class IsFourOfAKindRule() : Policy<Hand, HandRank>(
    Spec.Build(new HasNCardsWithTheSameRankProposition(4))
        .WhenTrue(HandRank.FourOfAKind)
        .WhenFalse(HandRank.Unknown)
        .Create("is a four of a kind hand"));
