namespace Karlssberg.Motiv.Poker.StraightHandSpecs;

public class IsJackHighStraightSpec() : Spec<Hand>(
    new DoesHandContainSpecifiedRanksSpec([Rank.Jack, Rank.Ten, Rank.Nine, Rank.Eight, Rank.Seven])
        .WhenTrue("is Jack High Straight")
        .WhenFalse("is Not Jack High Straight"));