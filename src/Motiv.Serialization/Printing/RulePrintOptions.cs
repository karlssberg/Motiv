namespace Motiv.Serialization;

/// <summary>
/// The options a live rule prints with: every compiled reference through the registry, the
/// registry's async names and known names, its collections for the rule's model named by element
/// type (no selector — the registry holds a delegate, not its source), and a class named after the
/// rule. What the reproducer and the csharp endpoint both print with, so the two agree.
/// </summary>
internal static class RulePrintOptions
{
    public static CSharpPrintOptions For(RuleBase rule, SpecRegistry registry, RuleSerializerOptions serializerOptions) =>
        For(rule.Name, rule.ModelType, registry, serializerOptions, "Rule");

    public static CSharpPrintOptions For(string name, Type modelType, SpecRegistry registry, RuleSerializerOptions serializerOptions, string classSuffix) => new()
    {
        ModelType = modelType,
        AsyncSpecs = new HashSet<string>(registry.Entries.Where(entry => entry.IsAsync).Select(entry => entry.Name), StringComparer.Ordinal),
        KnownSpecs = new HashSet<string>(registry.Entries.Select(entry => entry.Name), StringComparer.Ordinal),
        Collections = registry.Collections
            .Where(collection => collection.ParentType == modelType)
            .ToDictionary(collection => collection.Path, collection => new CSharpCollectionHandle(CSharpIdentifiers.TypeName(collection.ElementType), Selector: null), StringComparer.Ordinal),
        ClassName = CSharpIdentifiers.TypeName(name) + classSuffix,
        SerializerOptions = serializerOptions,
    };
}
