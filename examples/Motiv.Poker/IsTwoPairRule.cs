using Motiv.Poker.Models;
using Motiv.Poker.Propositions;
using static Motiv.Poker.Models.HandRank;

namespace Motiv.Poker;

public class IsTwoPairRule() : Policy<Hand, HandRank>(
    Spec.Build(new HasNPairsProposition(2))
        .WhenTrue(TwoPair)
        .WhenFalse(HighCard)
        .Create("is a two pair hand"));
