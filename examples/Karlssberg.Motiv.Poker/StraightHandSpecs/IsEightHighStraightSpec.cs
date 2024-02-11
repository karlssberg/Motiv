namespace Karlssberg.Motiv.Poker.StraightHandSpecs;

public class IsEightHighStraightSpec() : Spec<Hand>(
    new DoesHandContainSpecifiedRanksSpec([Rank.Eight, Rank.Seven, Rank.Six, Rank.Five, Rank.Four])
        .WhenTrue("is Eight High Straight")
        .WhenFalse("is Not Eight High Straight"));