#if !NET5_0_OR_GREATER
namespace System.Runtime.CompilerServices;

/// <summary>
/// The netstandard2.0 build's copy of the attribute .NET 5 ships, so that <see cref="Motiv.Throw.ThrowIfNull{T}" />
/// can name the argument it was called on there too. The compiler recognises the attribute by its full name, not by
/// the assembly it comes from.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
internal sealed class CallerArgumentExpressionAttribute(string parameterName) : Attribute
{
    public string ParameterName { get; } = parameterName;
}
#endif
