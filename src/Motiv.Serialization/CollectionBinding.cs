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

        return Reanchor(HigherOrder.Build(child, node.Operator, node.N));
    }

    /// <summary>
    /// Projects an already-built higher-order spec over the collection's elements onto the parent
    /// model, for any metadata type. Shared by the explanation loader's <see cref="BindHigherOrder"/>
    /// and by <see cref="MetadataRuleBinder{TMetadata}"/>'s typed-metadata higher-order binding.
    /// </summary>
    public SpecBase<TParent, TMetadata> Reanchor<TMetadata>(SpecBase<IEnumerable<TElement>, TMetadata> higherOrder) =>
        higherOrder.ChangeModelTo<TParent>(selector);
}
