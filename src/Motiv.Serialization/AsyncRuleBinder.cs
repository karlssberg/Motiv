namespace Motiv.Serialization;

internal static class AsyncRuleBinder
{
    public static AsyncSpecBase<TModel, string>? Bind<TModel>(
        RuleDocument document,
        SpecRegistry registry,
        List<RuleError> errors)
    {
        var root = BindNode<TModel>(document.Root!, registry, errors);
        if (root is null)
            return null;

        return document.Name is null ? root : Spec.Build(root).Create(document.Name);
    }

    public static AsyncSpecBase<TModel, string>? BindNode<TModel>(
        RuleNode node,
        SpecRegistry registry,
        List<RuleError> errors)
    {
        var spec = BindOperator<TModel>(node, registry, errors);

        // Reported independently of leaf/composition success, mirroring the parser's approach
        // of surfacing payload errors even when the operator subtree fails.
        var hasObjectPayloadError = ReportObjectPayloadError(node, errors);

        if (spec is null || hasObjectPayloadError)
            return null;

        return Decorate(node, spec);
    }

    public static AsyncSpecBase<TModel, string>? BindOperator<TModel>(
        RuleNode node,
        SpecRegistry registry,
        List<RuleError> errors) =>
        node.Operator switch
        {
            RuleOperator.Spec => BindSpecLeaf<TModel>(node, registry, errors),
            RuleOperator.Expression => BindExpressionLeaf<TModel>(node, errors),
            RuleOperator.Not => BindNode<TModel>(node.Children[0], registry, errors)?.Not(),
            _ when node.Operator.IsHigherOrder() => BindHigherOrder<TModel>(node, registry, errors),
            _ => BindComposition<TModel>(node, registry, errors)
        };

    private static bool ReportObjectPayloadError(RuleNode node, List<RuleError> errors)
    {
        if (!node.HasObjectPayloads)
            return false;

        errors.Add(new RuleError(node.Path, RuleErrorCode.MetadataTypeMismatch,
            "object 'whenTrue'/'whenFalse' payloads cannot be bound with explanation (string) semantics; " +
            "use a metadata load (DeserializeAsyncSpec<TModel, TMetadata>)"));
        return true;
    }

    private static AsyncSpecBase<TModel, string>? BindSpecLeaf<TModel>(
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
            if (entry.Spec is not AsyncSpecBase<TModel> asyncSpec)
            {
                errors.Add(new RuleError(node.Path, RuleErrorCode.ModelTypeMismatch,
                    $"'{node.SpecName}' has model type '{entry.ModelType.Name}' but the document is being " +
                    $"loaded for model type '{typeof(TModel).Name}'"));
                return null;
            }

            return ToExplanation(asyncSpec, entry);
        }

        if (entry.Spec is not SpecBase<TModel> spec)
        {
            errors.Add(new RuleError(node.Path, RuleErrorCode.ModelTypeMismatch,
                $"'{node.SpecName}' has model type '{entry.ModelType.Name}' but the document is being " +
                $"loaded for model type '{typeof(TModel).Name}'"));
            return null;
        }

        var explanation = entry.MetadataType == typeof(string)
            ? (SpecBase<TModel, string>)spec
            : spec.ToExplanationSpec();
        return explanation.ToAsyncSpec();
    }

    private static AsyncSpecBase<TModel, string> ToExplanation<TModel>(
        AsyncSpecBase<TModel> asyncSpec, SpecRegistryEntry entry) =>
        entry.MetadataType == typeof(string)
            ? (AsyncSpecBase<TModel, string>)asyncSpec
            : asyncSpec.ToAsyncExplanationSpec();

    private static AsyncSpecBase<TModel, string>? BindExpressionLeaf<TModel>(RuleNode node, List<RuleError> errors)
    {
        errors.Add(new RuleError(node.Path, RuleErrorCode.ExpressionsNotEnabled,
            "expression nodes require the Motiv.Serialization.Expressions package"));
        return null;
    }

    private static AsyncSpecBase<TModel, string>? Decorate<TModel>(
        RuleNode node,
        AsyncSpecBase<TModel, string> spec)
    {
        if (node.WhenTrueText is not null)
        {
            var builder = Spec.Build(spec).WhenTrue(node.WhenTrueText).WhenFalse(node.WhenFalseText!);
            return node.Name is null ? builder.Create() : builder.Create(node.Name);
        }

        return node.Name is null ? spec : Spec.Build(spec).Create(node.Name);
    }

    private static AsyncSpecBase<TModel, string>? BindComposition<TModel>(
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

    private static AsyncSpecBase<TModel, string>? BindHigherOrder<TModel>(
        RuleNode node, SpecRegistry registry, List<RuleError> errors) =>
        throw new NotImplementedException("Task 3");
}
