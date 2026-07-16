using System.Reflection;

namespace Motiv.Serialization;

internal static class RuleBinder
{
    private static readonly MethodInfo BindHigherOrderCoreMethod = typeof(RuleBinder)
        .GetMethod(nameof(BindHigherOrderCore), BindingFlags.NonPublic | BindingFlags.Static)!;

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
            _ when node.Operator.IsHigherOrder() => BindHigherOrder<TModel>(node, registry, errors),
            _ => BindComposition<TModel>(node, registry, errors)
        };

        // Reported independently of leaf/composition success, mirroring the parser's approach
        // of surfacing payload errors even when the operator subtree fails.
        var hasObjectPayloadError = ReportObjectPayloadError(node, errors);

        if (spec is null || hasObjectPayloadError)
            return null;

        return node.Operator.IsHigherOrder() ? spec : Decorate(node, spec);
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

    private static SpecBase<TModel, string>? BindHigherOrder<TModel>(
        RuleNode node,
        SpecRegistry registry,
        List<RuleError> errors)
    {
        var resolution = HigherOrderModelResolver.Resolve(typeof(TModel), node, errors);
        if (resolution is null)
            return null;

        return (SpecBase<TModel, string>?)BindHigherOrderCoreMethod
            .MakeGenericMethod(typeof(TModel), resolution.ElementType)
            .Invoke(null, [node, resolution, registry, errors]);
    }

    private static SpecBase<TModel, string>? BindHigherOrderCore<TModel, TElement>(
        RuleNode node,
        HigherOrderModelResolution resolution,
        SpecRegistry registry,
        List<RuleError> errors)
    {
        var inner = BindNode<TElement>(node.Children[0], registry, errors);
        if (inner is null)
            return null;

        return ReanchorToModel<TModel, TElement>(CreateHigherOrder(node, inner), resolution);
    }

    private static SpecBase<IEnumerable<TElement>, string> CreateHigherOrder<TElement>(
        RuleNode node,
        SpecBase<TElement, string> inner)
    {
        // Parse guarantees: string payloads arrive as a pair, bare nodes carry a name, and
        // N-forms have a resolved N by the time binding runs.
        if (node.WhenTrueText is { } whenTrue)
        {
            var whenFalse = node.WhenFalseText!;
            return (node.Operator, node.Name) switch
            {
                (RuleOperator.AsAllSatisfied, { } name) =>
                    Spec.Build(inner).AsAllSatisfied().WhenTrue(whenTrue).WhenFalse(whenFalse).Create(name),
                (RuleOperator.AsAllSatisfied, _) =>
                    Spec.Build(inner).AsAllSatisfied().WhenTrue(whenTrue).WhenFalse(whenFalse).Create(),
                (RuleOperator.AsAnySatisfied, { } name) =>
                    Spec.Build(inner).AsAnySatisfied().WhenTrue(whenTrue).WhenFalse(whenFalse).Create(name),
                (RuleOperator.AsAnySatisfied, _) =>
                    Spec.Build(inner).AsAnySatisfied().WhenTrue(whenTrue).WhenFalse(whenFalse).Create(),
                (RuleOperator.AsNSatisfied, { } name) =>
                    Spec.Build(inner).AsNSatisfied(node.N!.Value).WhenTrue(whenTrue).WhenFalse(whenFalse).Create(name),
                (RuleOperator.AsNSatisfied, _) =>
                    Spec.Build(inner).AsNSatisfied(node.N!.Value).WhenTrue(whenTrue).WhenFalse(whenFalse).Create(),
                (RuleOperator.AsAtLeastNSatisfied, { } name) =>
                    Spec.Build(inner).AsAtLeastNSatisfied(node.N!.Value).WhenTrue(whenTrue).WhenFalse(whenFalse).Create(name),
                (RuleOperator.AsAtLeastNSatisfied, _) =>
                    Spec.Build(inner).AsAtLeastNSatisfied(node.N!.Value).WhenTrue(whenTrue).WhenFalse(whenFalse).Create(),
                (RuleOperator.AsAtMostNSatisfied, { } name) =>
                    Spec.Build(inner).AsAtMostNSatisfied(node.N!.Value).WhenTrue(whenTrue).WhenFalse(whenFalse).Create(name),
                _ =>
                    Spec.Build(inner).AsAtMostNSatisfied(node.N!.Value).WhenTrue(whenTrue).WhenFalse(whenFalse).Create()
            };
        }

        return node.Operator switch
        {
            RuleOperator.AsAllSatisfied => Spec.Build(inner).AsAllSatisfied().Create(node.Name!),
            RuleOperator.AsAnySatisfied => Spec.Build(inner).AsAnySatisfied().Create(node.Name!),
            RuleOperator.AsNSatisfied => Spec.Build(inner).AsNSatisfied(node.N!.Value).Create(node.Name!),
            RuleOperator.AsAtLeastNSatisfied =>
                Spec.Build(inner).AsAtLeastNSatisfied(node.N!.Value).Create(node.Name!),
            _ => Spec.Build(inner).AsAtMostNSatisfied(node.N!.Value).Create(node.Name!)
        };
    }

    private static SpecBase<TModel, string> ReanchorToModel<TModel, TElement>(
        SpecBase<IEnumerable<TElement>, string> higherOrder,
        HigherOrderModelResolution resolution)
    {
        if (resolution.Properties.Length > 0)
            return higherOrder.ChangeModelTo<TModel>(model =>
                (IEnumerable<TElement>)resolution.GetCollection(model!));

        return typeof(TModel) == typeof(IEnumerable<TElement>)
            ? (SpecBase<TModel, string>)(object)higherOrder
            : higherOrder.ChangeModelTo<TModel>(model => (IEnumerable<TElement>)(object)model!);
    }
}
