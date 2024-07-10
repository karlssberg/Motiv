using Motiv.Poker.Models;
using Motiv.Poker.Propositions;

namespace Motiv.Poker;

public class IsFullHouseRule() : Policy<Hand, HandRank>(() =>
    Spec.Build(new HasNCardsWithTheSameRankProposition(2) & new HasNCardsWithTheSameRankProposition(3))
        .WhenTrue(HandRank.FullHouse)
        .WhenFalse(HandRank.HighCard)
        .Create("is a full house hand"));
