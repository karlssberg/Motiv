using System.Reflection;
using System.Text.Json;

namespace Motiv.Serialization;

internal sealed class MetadataRuleBinder<TMetadata>(SpecRegistry registry, RuleSerializerOptions options)
{
    private static readonly MethodInfo BindHigherOrderCoreMethod = typeof(MetadataRuleBinder<TMetadata>)
        .GetMethod(nameof(BindHigherOrderCore), BindingFlags.NonPublic | BindingFlags.Instance)!;

    public SpecBase<TModel, TMetadata>? Bind<TModel>(RuleDocument document, List<RuleError> errors)
    {
        var root = BindNode<TModel>(document.Root!, errors);
        if (root is null)
            return null;

        return document.Name is null ? root : Spec.Build(root).Create(document.Name);
    }

    private SpecBase<TModel, TMetadata>? BindNode<TModel>(RuleNode node, List<RuleError> errors)
    {
        if (node.WhenTrueText is not null)
        {
            errors.Add(new RuleError(node.Path, RuleErrorCode.MetadataTypeMismatch,
                $"string 'whenTrue'/'whenFalse' payloads cannot supply metadata of type " +
                $"'{typeof(TMetadata).Name}'; use object payloads"));
            return null;
        }

        if (node.Operator.IsHigherOrder())
            return BindHigherOrder<TModel>(node, errors);

        if (node.HasObjectPayloads)
            return BindRemetadatized<TModel>(node, errors);

        var spec = node.Operator switch
        {
            RuleOperator.Spec => BindSpecLeaf<TModel>(node, errors),
            RuleOperator.Expression => BindExpressionLeaf<TModel>(node, errors),
            RuleOperator.Not => BindNode<TModel>(node.Children[0], errors)?.Not(),
            _ => BindComposition<TModel>(node, errors)
        };

        if (spec is null)
            return null;

        return node.Name is null ? spec : Spec.Build(spec).Create(node.Name);
    }

    private SpecBase<TModel, TMetadata>? BindRemetadatized<TModel>(RuleNode node, List<RuleError> errors)
    {
        // Parse guarantees a name accompanies object payloads. The operator subtree binds with
        // explanation semantics; the payloads then re-metadatize it, exactly like
        // Spec.Build(spec).WhenTrue(obj).WhenFalse(obj).Create(name) does in core.
        var errorCountBefore = errors.Count;
        var whenTrue = DeserializePayload(node.WhenTrueElement!.Value, $"{node.Path}.whenTrue", errors);
        var whenFalse = DeserializePayload(node.WhenFalseElement!.Value, $"{node.Path}.whenFalse", errors);
        var underlying = RuleBinder.BindOperator<TModel>(node, registry, errors);

        if (underlying is null || errors.Count > errorCountBefore)
            return null;

        return Spec.Build(underlying).WhenTrue(whenTrue!).WhenFalse(whenFalse!).Create(node.Name!);
    }

    private SpecBase<TModel, TMetadata>? BindSpecLeaf<TModel>(RuleNode node, List<RuleError> errors)
    {
        var entry = registry.Find(node.SpecName!);
        if (entry is null)
        {
            errors.Add(new RuleError(node.Path, RuleErrorCode.UnknownSpec,
                $"no spec is registered under the name '{node.SpecName}'"));
            return null;
        }

        if (entry.IsAsync)
        {
            errors.Add(new RuleError(node.Path, RuleErrorCode.AsyncSpecInSyncLoad,
                $"'{node.SpecName}' is an async spec; use DeserializeAsyncSpec to load this document"));
            return null;
        }

        if (entry.Spec is not SpecBase<TModel> spec)
        {
            errors.Add(new RuleError(node.Path, RuleErrorCode.ModelTypeMismatch,
                $"'{node.SpecName}' has model type '{entry.ModelType.Name}' but the document is being " +
                $"loaded for model type '{typeof(TModel).Name}'"));
            return null;
        }

        if (spec is not SpecBase<TModel, TMetadata> typed)
        {
            errors.Add(new RuleError(node.Path, RuleErrorCode.MetadataTypeMismatch,
                $"'{node.SpecName}' has metadata type '{entry.MetadataType.Name}' but the load expects " +
                $"'{typeof(TMetadata).Name}'; decorate the node with object 'whenTrue'/'whenFalse' " +
                "payloads or register a matching spec"));
            return null;
        }

        return typed;
    }

    private static SpecBase<TModel, TMetadata>? BindExpressionLeaf<TModel>(
        RuleNode node,
        List<RuleError> errors)
    {
        errors.Add(new RuleError(node.Path, RuleErrorCode.ExpressionsNotEnabled,
            "expression nodes require the Motiv.Serialization.Expressions package"));
        return null;
    }

    private SpecBase<TModel, TMetadata>? BindComposition<TModel>(RuleNode node, List<RuleError> errors)
    {
        var children = node.Children
            .Select(child => BindNode<TModel>(child, errors))
            .ToArray();

        if (children.Any(child => child is null))
            return null;

        return children.Aggregate((left, right) => node.Operator switch
        {
            RuleOperator.And => left!.And(right!),
            RuleOperator.Or => left!.Or(right!),
            RuleOperator.XOr => left!.XOr(right!),
            RuleOperator.AndAlso => left!.AndAlso(right!),
            _ => left!.OrElse(right!)
        });
    }

    private SpecBase<TModel, TMetadata>? BindHigherOrder<TModel>(RuleNode node, List<RuleError> errors)
    {
        // Unlike the string/explanation loader, a metadata load cannot synthesize a default
        // WhenTrue/WhenFalse for an arbitrary TMetadata, so Create(node.Name!) below requires a
        // name whenever there is no object payload to re-metadatize with instead.
        if (!node.HasObjectPayloads && node.Name is null)
        {
            errors.Add(new RuleError(node.Path, RuleErrorCode.MetadataTypeMismatch,
                "a higher-order node used in a metadata load must declare a 'name' or object " +
                "'whenTrue'/'whenFalse' payloads"));
            return null;
        }

        var binding = registry.FindCollection<TModel>(node.PathText!);
        if (binding is null)
        {
            errors.Add(new RuleError(node.Path, RuleErrorCode.UnknownCollection,
                $"no collection is registered at path '{node.PathText}' for model '{typeof(TModel).Name}'"));
            return null;
        }

        return (SpecBase<TModel, TMetadata>?)BindHigherOrderCoreMethod
            .MakeGenericMethod(typeof(TModel), binding.ElementType)
            .Invoke(this, [node, binding, errors]);
    }

    private SpecBase<TModel, TMetadata>? BindHigherOrderCore<TModel, TElement>(
        RuleNode node,
        CollectionBinding<TModel> binding,
        List<RuleError> errors)
    {
        var typedBinding = (CollectionBinding<TModel, TElement>)binding;

        if (node.HasObjectPayloads)
        {
            var errorCountBefore = errors.Count;
            var whenTrue = DeserializePayload(node.WhenTrueElement!.Value, $"{node.Path}.whenTrue", errors);
            var whenFalse = DeserializePayload(node.WhenFalseElement!.Value, $"{node.Path}.whenFalse", errors);
            var inner = RuleBinder.BindNode<TElement>(node.Children[0], registry, errors);

            if (inner is null || errors.Count > errorCountBefore)
                return null;

            return typedBinding.Reanchor(CreateRemetadatizedHigherOrder(node, inner, whenTrue!, whenFalse!));
        }

        var innerTyped = BindNode<TElement>(node.Children[0], errors);
        if (innerTyped is null)
            return null;

        return typedBinding.Reanchor(CreateHigherOrder(node, innerTyped));
    }

    private static SpecBase<IEnumerable<TElement>, TMetadata> CreateRemetadatizedHigherOrder<TElement>(
        RuleNode node,
        SpecBase<TElement, string> inner,
        TMetadata whenTrue,
        TMetadata whenFalse) =>
        node.Operator switch
        {
            RuleOperator.AsAllSatisfied =>
                Spec.Build(inner).AsAllSatisfied().WhenTrue(whenTrue).WhenFalse(whenFalse).Create(node.Name!),
            RuleOperator.AsAnySatisfied =>
                Spec.Build(inner).AsAnySatisfied().WhenTrue(whenTrue).WhenFalse(whenFalse).Create(node.Name!),
            RuleOperator.AsNSatisfied =>
                Spec.Build(inner).AsNSatisfied(node.N!.Value).WhenTrue(whenTrue).WhenFalse(whenFalse).Create(node.Name!),
            RuleOperator.AsAtLeastNSatisfied =>
                Spec.Build(inner).AsAtLeastNSatisfied(node.N!.Value).WhenTrue(whenTrue).WhenFalse(whenFalse).Create(node.Name!),
            _ =>
                Spec.Build(inner).AsAtMostNSatisfied(node.N!.Value).WhenTrue(whenTrue).WhenFalse(whenFalse).Create(node.Name!)
        };

    private static SpecBase<IEnumerable<TElement>, TMetadata> CreateHigherOrder<TElement>(
        RuleNode node,
        SpecBase<TElement, TMetadata> inner) =>
        node.Operator switch
        {
            RuleOperator.AsAllSatisfied => Spec.Build(inner).AsAllSatisfied().Create(node.Name!),
            RuleOperator.AsAnySatisfied => Spec.Build(inner).AsAnySatisfied().Create(node.Name!),
            RuleOperator.AsNSatisfied => Spec.Build(inner).AsNSatisfied(node.N!.Value).Create(node.Name!),
            RuleOperator.AsAtLeastNSatisfied =>
                Spec.Build(inner).AsAtLeastNSatisfied(node.N!.Value).Create(node.Name!),
            _ => Spec.Build(inner).AsAtMostNSatisfied(node.N!.Value).Create(node.Name!)
        };

    private TMetadata? DeserializePayload(JsonElement element, string path, List<RuleError> errors)
    {
        try
        {
            var value = element.Deserialize<TMetadata>(options.MetadataJsonOptions);
            if (value is not null)
                return value;

            errors.Add(new RuleError(path, RuleErrorCode.MetadataTypeMismatch,
                $"payload deserialized to null for metadata type '{typeof(TMetadata).Name}'"));
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            errors.Add(new RuleError(path, RuleErrorCode.MetadataTypeMismatch,
                $"payload could not be deserialized to metadata type '{typeof(TMetadata).Name}': " +
                exception.Message));
        }

        return default;
    }
}
