namespace Karlssberg.Motiv.Poker.StraightHands;

public class IsAceHighStraightBroadwaySpec() : Spec<Hand>(
    new DoesHandContainSpecifiedRanksSpec([Rank.Ace, Rank.King, Rank.Queen, Rank.Jack, Rank.Ten])
        .YieldWhenTrue("Is Ace High Straight Broadway")
        .YieldWhenFalse("Is Not Ace High Straight Broadway")
        .CreateSpec());
        