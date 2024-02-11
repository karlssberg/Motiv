namespace Karlssberg.Motiv.Poker.StraightHandSpecs;

public class IsTenHighStraightSpec() : Spec<Hand>(
    new DoesHandContainSpecifiedRanksSpec([Rank.Ten, Rank.Nine, Rank.Eight, Rank.Seven, Rank.Six])
        .WhenTrue("is Ten High Straight")
        .WhenFalse("is Not Ten High Straight"));