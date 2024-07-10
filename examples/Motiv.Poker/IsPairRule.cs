using Motiv.Poker.Models;
using Motiv.Poker.Propositions;

namespace Motiv.Poker;

public class IsPairRule() : Policy<Hand, HandRank>(
    Spec.Build(new HasNPairsProposition(1))
        .WhenTrue(HandRank.Pair)
        .WhenFalse(HandRank.HighCard)
        .Create("is a pair hand"));
