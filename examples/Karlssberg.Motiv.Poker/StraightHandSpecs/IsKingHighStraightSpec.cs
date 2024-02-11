namespace Karlssberg.Motiv.Poker.StraightHandSpecs;

public class IsKingHighStraightSpec() : Spec<Hand>(
    new DoesHandContainSpecifiedRanksSpec([Rank.King, Rank.Queen, Rank.Jack, Rank.Ten, Rank.Nine])
        .WhenTrue("is King High Straight")
        .WhenFalse("is Not King High Straight"));