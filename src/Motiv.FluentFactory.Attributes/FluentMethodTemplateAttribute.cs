using System.Diagnostics.CodeAnalysis;

namespace Motiv.FluentFactory.Attributes;

/// <summary>
/// Marks a method to be used as a template for generating fluent methods.
/// </summary>
[ExcludeFromCodeCoverage]
[AttributeUsage(AttributeTargets.Method)]
public class FluentMethodTemplateAttribute : Attribute
{
}
