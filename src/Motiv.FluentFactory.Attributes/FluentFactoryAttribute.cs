using System.Diagnostics.CodeAnalysis;

namespace Motiv.FluentFactory.Attributes;

/// <summary>
/// Marks a class or struct to be used as a fluent factory.
/// </summary>
[ExcludeFromCodeCoverage]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class FluentFactoryAttribute : Attribute
{
}
