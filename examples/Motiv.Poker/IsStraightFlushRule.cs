using Motiv.Poker.Models;

namespace Motiv.Poker;

public class IsStraightFlushRule() : Policy<Hand, HandRank>(
    Spec.Build(new IsStraightRule() & new IsFlushRuleRule())
        .WhenTrue(HandRank.StraightFlush)
        .WhenFalse(HandRank.Unknown)
        .Create("is a straight flush hand"));
