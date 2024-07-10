using System.ComponentModel;

namespace Motiv.Poker.Models;

public enum Suit
{
    [Description("C")]
    Clubs,
    [Description("D")]
    Diamonds,
    [Description("H")]
    Hearts,
    [Description("S")]
    Spades
}