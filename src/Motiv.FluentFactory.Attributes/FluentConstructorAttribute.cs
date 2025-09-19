using System.Diagnostics.CodeAnalysis;

namespace Motiv.FluentFactory.Attributes;

/// <summary>
/// Marks a constructor, class or struct to be used as a fluent factory for the specified root type.
/// </summary>
/// <param name="rootType">The type to create instances of.</param>
[ExcludeFromCodeCoverage]
[AttributeUsage(AttributeTargets.Constructor | AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
public sealed class FluentConstructorAttribute(Type rootType) : Attribute
{
    /// <summary>
    /// The type to create instances of.
    /// </summary>
    public Type RootType { get; } = rootType;

    /// <summary>
    /// Options for the fluent factory.
    /// </summary>
    public FluentOptions Options { get; set; }

    /// <summary>
    /// The name of the method to create an instance. If not set, "Create" will be used.
    /// </summary>
    public string? CreateMethodName { get; set; }
}
