using System.Diagnostics.CodeAnalysis;

namespace Motiv.FluentFactory.Generator;

[ExcludeFromCodeCoverage]
[AttributeUsage(AttributeTargets.Constructor | AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
public sealed class FluentConstructorAttribute(Type rootType) : Attribute
{
    public Type RootType { get; } = rootType;

    public FluentOptions Options { get; set; }

    public string? CreateMethodName { get; set; }
}
