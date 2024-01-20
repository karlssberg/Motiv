namespace Karlssberg.Motiv.Poker.HandRankSpecs;

public class IsHandFlushSpec() : Spec<Hand, HandRank>(
    new HasNCardsWithTheSameSuitSpec(5)
        .YieldWhenTrue(HandRank.Flush)
        .YieldWhenFalse(HandRank.HighCard)
        .CreateSpec("is a Flush hand"));