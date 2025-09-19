namespace Motiv.FluentFactory.Attributes;

/// <summary>
/// Options for the fluent factory generation.
/// </summary>
[Flags]
public enum FluentOptions
{
    /// <summary>
    /// No options are specified.
    /// </summary>
    None = 0,

    /// <summary>
    /// Ensures that the <c>Create()</c> step is not generated for the current constructor.  This means the constructor
    /// will be called immediately once all the parameters have been resolved.  Because other constructors may
    /// extend this constructors parameter sequence, the containing type must have the <c>partial</c> modifier applied.
    /// This is so that the generator can continue creating the fluent step methods beyond the present sequence.
    /// </summary>
    NoCreateMethod = 1,
}
