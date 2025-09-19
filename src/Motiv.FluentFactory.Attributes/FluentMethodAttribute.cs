using System.Diagnostics.CodeAnalysis;

namespace Motiv.FluentFactory.Attributes;

/// <summary>
/// Marks a parameter to be used as a method in the fluent factory.
/// </summary>
/// <param name="methodName">The name of the method to create an instance.</param>
[ExcludeFromCodeCoverage]
[AttributeUsage(AttributeTargets.Parameter)]
public class FluentMethodAttribute(string methodName) : Attribute
{
    /// <summary>
    /// The name of the method to create an instance.
    /// </summary>
    public string MethodName { get; } = methodName;

    /// <summary>
    /// The priority of the method. Methods with lower priority will be generated first. Default is 0.
    /// </summary>
    public int Priority { get; set; }
}
