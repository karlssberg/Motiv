namespace Motiv.Serialization;

internal static class RuleBinder
{
    public static SpecBase<TModel, string>? Bind<TModel>(
        RuleDocument document,
        SpecRegistry registry,
        List<RuleError> errors)
    {
        var root = BindNode<TModel>(document.Root!, registry, errors);
        if (root is null)
            return null;

        return document.Name is null ? root : Spec.Build(root).Create(document.Name);
    }

    private static SpecBase<TModel, string>? BindNode<TModel>(
        RuleNode node,
        SpecRegistry registry,
        List<RuleError> errors)
    {
        var spec = node.Operator switch
        {
            RuleOperator.Spec => BindSpecLeaf<TModel>(node, registry, errors),
            RuleOperator.Expression => BindExpressionLeaf<TModel>(node, errors),
            RuleOperator.Not => BindNode<TModel>(node.Children[0], registry, errors)?.Not(),
            _ => BindComposition<TModel>(node, registry, errors)
        };

        // Reported independently of leaf/composition success, mirroring the parser's approach
        // of surfacing payload errors even when the operator subtree fails.
        var hasObjectPayloadError = ReportObjectPayloadError(node, errors);

        return spec is null || hasObjectPayloadError ? null : Decorate(node, spec);
    }

    private static bool ReportObjectPayloadError(RuleNode node, List<RuleError> errors)
    {
        if (!node.HasObjectPayloads)
            return false;

        errors.Add(new RuleError(node.Path, RuleErrorCode.MetadataTypeMismatch,
            "object 'whenTrue'/'whenFalse' payloads require a metadata load; this is an explanation load"));
        return true;
    }

    private static SpecBase<TModel, string>? BindSpecLeaf<TModel>(
        RuleNode node,
        SpecRegistry registry,
        List<RuleError> errors)
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

        return entry.MetadataType == typeof(string)
            ? (SpecBase<TModel, string>)spec
            : spec.ToExplanationSpec();
    }

    private static SpecBase<TModel, string>? BindExpressionLeaf<TModel>(RuleNode node, List<RuleError> errors)
    {
        errors.Add(new RuleError(node.Path, RuleErrorCode.ExpressionsNotEnabled,
            "expression nodes require the Motiv.Serialization.Expressions package"));
        return null;
    }

    private static SpecBase<TModel, string>? Decorate<TModel>(
        RuleNode node,
        SpecBase<TModel, string> spec)
    {
        if (node.WhenTrueText is not null)
        {
            var builder = Spec.Build(spec).WhenTrue(node.WhenTrueText).WhenFalse(node.WhenFalseText!);
            return node.Name is null ? builder.Create() : builder.Create(node.Name);
        }

        return node.Name is null ? spec : Spec.Build(spec).Create(node.Name);
    }

    private static SpecBase<TModel, string>? BindComposition<TModel>(
        RuleNode node,
        SpecRegistry registry,
        List<RuleError> errors)
    {
        var children = node.Children
            .Select(child => BindNode<TModel>(child, registry, errors))
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
}
