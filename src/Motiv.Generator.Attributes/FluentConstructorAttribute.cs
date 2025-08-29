namespace Motiv.Generator.Attributes;

[AttributeUsage(AttributeTargets.Constructor | AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
public sealed class FluentConstructorAttribute(Type rootType) : Attribute
{
    public Type RootType { get; } = rootType;

    public FluentOptions Options { get; set; }
}
