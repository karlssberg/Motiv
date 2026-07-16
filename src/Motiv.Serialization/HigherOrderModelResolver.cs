namespace Motiv.Serialization;

internal static class HigherOrderModelResolver
{
    public static HigherOrderModelResolution? Resolve(Type modelType, RuleNode node, List<RuleError> errors)
    {
        var elementType = FindElementType(modelType);
        if (elementType is not null)
            return new HigherOrderModelResolution(elementType, []);

        errors.Add(new RuleError(node.Path, RuleErrorCode.InvalidHigherOrderPath,
            $"'{modelType.Name}' is not a collection; a higher-order node needs an IEnumerable<T> " +
            "model or a 'path' that selects one"));
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
