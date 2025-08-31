namespace Motiv.Generator;

[Flags]
public enum Modifiers
{
    None = 0,
    Public = 1,
    Protected = 2,
    Internal = 4,
    Private = 8,
    Static = 16,
    Partial = 32,
}
