namespace Karlssberg.Motiv.Poker.StraightHandSpecs;

public class IsSevenHighStraightSpec() : Spec<Hand>(
    new DoesHandContainSpecifiedRanksSpec([Rank.Seven, Rank.Six, Rank.Five, Rank.Four, Rank.Three])
        .YieldWhenTrue("is Seven High Straight")
        .YieldWhenFalse("is Not Seven High Straight"));