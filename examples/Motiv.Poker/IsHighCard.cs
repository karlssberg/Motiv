using Motiv.Poker.Models;

namespace Motiv.Poker;

public class IsHighCardRule() : Policy<Hand, HandRank>(
    Spec.Build((Hand _) => true)
        .WhenTrue(HandRank.HighCard)
        .WhenFalse(HandRank.Unknown)
        .Create("is a high card hand"));
