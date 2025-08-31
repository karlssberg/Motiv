using System.Diagnostics.CodeAnalysis;

namespace Motiv.FluentFactory.Generator;

[ExcludeFromCodeCoverage]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class FluentFactoryAttribute : Attribute;
