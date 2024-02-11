namespace Karlssberg.Motiv.Poker.StraightHandSpecs;

public class IsSevenHighStraightSpec() : Spec<Hand>(
    new DoesHandContainSpecifiedRanksSpec([Rank.Seven, Rank.Six, Rank.Five, Rank.Four, Rank.Three])
        .WhenTrue("is Seven High Straight")
        .WhenFalse("is Not Seven High Straight"));