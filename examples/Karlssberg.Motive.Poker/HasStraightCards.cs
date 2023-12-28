namespace Karlssberg.Motive.Poker;

public class HasStraightCards() : Spec<Hand>(
    hand => hand.Ranks.Distinct().Count() is 5
            && hand.Ranks.Max() - hand.Ranks.Min() is 4 or 12,
    "Is Straight",
    "Is Not Straight");