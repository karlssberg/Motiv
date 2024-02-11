namespace Karlssberg.Motiv.Poker.StraightHandSpecs;

public class IsQueenHighStraightSpec() : Spec<Hand>(
    new DoesHandContainSpecifiedRanksSpec([Rank.Queen, Rank.Jack, Rank.Ten, Rank.Nine, Rank.Eight])
        .WhenTrue("is Queen High Straight")
        .WhenFalse("is Not Queen High Straight"));