using System.Reflection;

namespace Motiv.Serialization;

internal sealed class HigherOrderModelResolution(Type elementType, PropertyInfo[] properties)
{
    public Type ElementType { get; } = elementType;

    public PropertyInfo[] Properties { get; } = properties;

    public object GetCollection(object model)
    {
        var current = model;
        foreach (var property in Properties)
            current = property.GetValue(current)
                ?? throw new InvalidOperationException(
                    $"'{property.DeclaringType!.Name}.{property.Name}' returned null while selecting " +
                    "the higher-order collection");
        return current;
    }
}
