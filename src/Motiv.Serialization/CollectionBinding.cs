namespace Motiv.Serialization;

/// <summary>
/// A host-registered projection from a parent model to one of its collections, able to bind a
/// higher-order rule node that operates over that collection. Keeps the element type a compile-time
/// generic argument (captured at registration), so binding needs no reflection.
/// </summary>
internal abstract class CollectionBinding<TParent>
{
    public abstract Type ElementType { get; }

    public abstract SpecBase<TParent, string>? BindHigherOrder(
        RuleNode node, SpecRegistry registry, List<RuleError> errors);
}

internal sealed class CollectionBinding<TParent, TElement>(Func<TParent, IEnumerable<TElement>> selector)
    : CollectionBinding<TParent>
{
    public override Type ElementType => typeof(TElement);

    public override SpecBase<TParent, string>? BindHigherOrder(
        RuleNode node, SpecRegistry registry, List<RuleError> errors)
    {
        var child = RuleBinder.BindElement<TElement>(node.Children[0], registry, errors);
        if (child is null)
            return null;

        var higherOrder = HigherOrder.Build(child, node.Operator, node.N);
        return higherOrder.ChangeModelTo<TParent>(selector);
    }
}
