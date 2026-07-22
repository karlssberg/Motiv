using System.Text.Json;

namespace Motiv.Serialization;

internal sealed class AsyncMetadataRuleBinder<TMetadata>(SpecRegistry registry, RuleSerializerOptions options)
{
    public AsyncSpecBase<TModel, TMetadata>? Bind<TModel>(RuleDocument document, List<RuleError> errors)
    {
        var root = BindNode<TModel>(document.Root!, errors);
        if (root is null)
            return null;

        return document.Name is null ? root : Spec.Build(root).Create(document.Name);
    }

    private AsyncSpecBase<TModel, TMetadata>? BindNode<TModel>(RuleNode node, List<RuleError> errors)
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

    private AsyncSpecBase<TModel, TMetadata>? BindRemetadatized<TModel>(RuleNode node, List<RuleError> errors)
    {
        // Parse guarantees a name accompanies object payloads. The operator subtree binds with
        // explanation semantics; the payloads then re-metadatize it, exactly like
        // Spec.Build(spec).WhenTrue(obj).WhenFalse(obj).Create(name) does in core.
        var errorCountBefore = errors.Count;
        var whenTrue = DeserializePayload(node.WhenTrueElement!.Value, $"{node.Path}.whenTrue", errors);
        var whenFalse = DeserializePayload(node.WhenFalseElement!.Value, $"{node.Path}.whenFalse", errors);
        var underlying = AsyncRuleBinder.BindOperator<TModel>(node, registry, errors);

        if (underlying is null || errors.Count > errorCountBefore)
            return null;

        return Spec.Build(underlying).WhenTrue(whenTrue!).WhenFalse(whenFalse!).Create(node.Name!);
    }

    private AsyncSpecBase<TModel, TMetadata>? BindSpecLeaf<TModel>(RuleNode node, List<RuleError> errors)
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
            if (entry.Spec is not AsyncSpecBase<TModel> asyncSpec)
            {
                errors.Add(new RuleError(node.Path, RuleErrorCode.ModelTypeMismatch,
                    $"'{node.SpecName}' has model type '{entry.ModelType.Name}' but the document is being " +
                    $"loaded for model type '{typeof(TModel).Name}'"));
                return null;
            }

            if (asyncSpec is not AsyncSpecBase<TModel, TMetadata> asyncTyped)
            {
                errors.Add(new RuleError(node.Path, RuleErrorCode.MetadataTypeMismatch,
                    $"'{node.SpecName}' has metadata type '{entry.MetadataType.Name}' but the load expects " +
                    $"'{typeof(TMetadata).Name}'; decorate the node with object 'whenTrue'/'whenFalse' " +
                    "payloads or register a matching spec"));
                return null;
            }

            return asyncTyped;
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

        return typed.ToAsyncSpec();
    }

    private static AsyncSpecBase<TModel, TMetadata>? BindExpressionLeaf<TModel>(
        RuleNode node,
        List<RuleError> errors)
    {
        errors.Add(new RuleError(node.Path, RuleErrorCode.ExpressionsNotEnabled,
            "expression nodes require the Motiv.Serialization.Expressions package"));
        return null;
    }

    private AsyncSpecBase<TModel, TMetadata>? BindComposition<TModel>(RuleNode node, List<RuleError> errors)
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

    private AsyncSpecBase<TModel, TMetadata>? BindHigherOrder<TModel>(RuleNode node, List<RuleError> errors)
    {
        // Core has no async quantifiers: the whole higher-order node binds synchronously through
        // the sync metadata binder (which also enforces its name-or-payload rule) and lifts. An
        // async leaf inside it is therefore a distinct, actionable error.
        var errorCountBefore = errors.Count;
        var spec = new MetadataRuleBinder<TMetadata>(registry, options).BindNode<TModel>(node, errors);

        for (var i = errorCountBefore; i < errors.Count; i++)
        {
            if (errors[i].Code == RuleErrorCode.AsyncSpecInSyncLoad)
                errors[i] = new RuleError(errors[i].Path, RuleErrorCode.AsyncSpecInHigherOrder,
                    "async specs cannot be used inside a higher-order subtree; " +
                    "higher-order propositions evaluate synchronously");
        }

        return spec?.ToAsyncSpec();
    }

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
