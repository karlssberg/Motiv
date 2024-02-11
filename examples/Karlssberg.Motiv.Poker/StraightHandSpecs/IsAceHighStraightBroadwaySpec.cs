namespace Karlssberg.Motiv.Poker.StraightHandSpecs;

public class IsAceHighStraightBroadwaySpec() : Spec<Hand>(
    new DoesHandContainSpecifiedRanksSpec([Rank.Ace, Rank.King, Rank.Queen, Rank.Jack, Rank.Ten])
        .WhenTrue("Is Ace High Straight Broadway")
        .WhenFalse("Is Not Ace High Straight Broadway"));