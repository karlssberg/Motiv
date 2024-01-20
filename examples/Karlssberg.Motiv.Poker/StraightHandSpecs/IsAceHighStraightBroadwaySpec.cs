namespace Karlssberg.Motiv.Poker.StraightHandSpecs;

public class IsAceHighStraightBroadwaySpec() : Spec<Hand>(
    new DoesHandContainSpecifiedRanksSpec([Rank.Ace, Rank.King, Rank.Queen, Rank.Jack, Rank.Ten])
        .YieldWhenTrue("Is Ace High Straight Broadway")
        .YieldWhenFalse("Is Not Ace High Straight Broadway")
        .CreateSpec());