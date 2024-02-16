﻿namespace Karlssberg.Motiv.Poker.HandRankSpecs;

public class IsHandThreeOfAKindSpec() : Spec<Hand, HandRank>(
    Spec.Build(new HasNCardsWithTheSameRankSpec(3))
        .WhenTrue(HandRank.ThreeOfAKind)
        .WhenFalse(HandRank.HighCard)
        .CreateSpec("is a three of a kind hand"));