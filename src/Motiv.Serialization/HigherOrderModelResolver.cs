using System.Reflection;

namespace Motiv.Serialization;

internal static class HigherOrderModelResolver
{
    public static HigherOrderModelResolution? Resolve(Type modelType, RuleNode node, List<RuleError> errors)
    {
        var properties = new List<PropertyInfo>();
        var current = modelType;

        if (node.PathText is { } pathText)
        {
            foreach (var segment in pathText.Split('.'))
            {
                var property = current.GetProperty(segment, BindingFlags.Public | BindingFlags.Instance);
                if (property is null)
                {
                    errors.Add(new RuleError($"{node.Path}.path", RuleErrorCode.InvalidHigherOrderPath,
                        $"'{current.Name}' has no public instance property '{segment}'"));
                    return null;
                }

                properties.Add(property);
                current = property.PropertyType;
            }
        }

        var elementType = FindElementType(current);
        if (elementType is not null)
            return new HigherOrderModelResolution(elementType, [.. properties]);

        errors.Add(node.PathText is null
            ? new RuleError(node.Path, RuleErrorCode.InvalidHigherOrderPath,
                $"'{current.Name}' is not a collection; a higher-order node needs an IEnumerable<T> " +
                "model or a 'path' that selects one")
            : new RuleError($"{node.Path}.path", RuleErrorCode.InvalidHigherOrderPath,
                $"'{node.PathText}' selects '{current.Name}', which is not a collection (IEnumerable<T>)"));
        return null;
    }

    private static Type? FindElementType(Type type)
    {
        if (IsEnumerableInterface(type))
            return type.GetGenericArguments()[0];

        return type.GetInterfaces()
            .Where(IsEnumerableInterface)
            .Select(candidate => candidate.GetGenericArguments()[0])
            .FirstOrDefault();
    }

    private static bool IsEnumerableInterface(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>);
}
