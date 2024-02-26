namespace Karlssberg.Motiv;

internal interface ICompositeAssertion
{
    /// <summary>Gets the description of the XOR operation.</summary>
    IAssertion Assertion { get; }
}