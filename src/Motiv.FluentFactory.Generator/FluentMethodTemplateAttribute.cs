using System.Diagnostics.CodeAnalysis;

namespace Motiv.FluentFactory.Generator;

[ExcludeFromCodeCoverage]
[AttributeUsage(AttributeTargets.Method)]
public class FluentMethodTemplateAttribute : Attribute;
