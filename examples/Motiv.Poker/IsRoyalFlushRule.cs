using Motiv.Poker.Models;
using Motiv.Poker.StraightHand;

namespace Motiv.Poker;

public class IsRoyalFlushRule() : Policy<Hand, HandRank>(() => Spec
    .Build(new IsFlushRuleRule() & new IsAceHighStraightBroadwayProposition())
    .WhenTrue(HandRank.RoyalFlush)
    .WhenFalse(HandRank.HighCard)
    .Create("is a royal flush hand"));
