using Motiv.Poker.Models;

namespace Motiv.Poker;

public class PokerRules() : Policy<Hand, HandRank>(
    new PolicyBase<Hand, HandRank>[]
    {
        new IsRoyalFlushRule(),
        new IsStraightFlushRule(),
        new IsFourOfAKindRule(),
        new IsFullHouseRule(),
        new IsFlushRuleRule(),
        new IsStraightRule(),
        new IsThreeOfAKindRule(),
        new IsTwoPairRule(),
        new IsPairRule(),
        new IsHighCardRule()
    }.OrElseTogether());
