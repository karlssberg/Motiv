namespace Karlssberg.Motiv.Poker.StraightHandSpecs;

public class IsNineHighStraightSpec() : Spec<Hand>(
    new DoesHandContainSpecifiedRanksSpec([Rank.Nine, Rank.Eight, Rank.Seven, Rank.Six, Rank.Five])
        .WhenTrue("is Nine High Straight")
        .WhenFalse("is Not Nine High Straight"));