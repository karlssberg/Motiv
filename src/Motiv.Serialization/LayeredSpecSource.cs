namespace Motiv.Serialization;

/// <summary>
/// Resolves names against runtime-authored propositions first, then whatever lies beneath —
/// the compiled registry for a live generation, or the live source itself when a reproduction
/// layers pinned proposition versions over it.
/// </summary>
/// <remarks>
/// The layering *is* the override mechanism, and it is what makes revert free: an authored
/// proposition that is removed from the overlay stops shadowing, and the compiled entry — which was
/// never copied or moved — resolves again. It also keeps <see cref="SpecRegistry"/> an honest record
/// of what the developer compiled in, which a mutable registry could not be.
/// </remarks>
internal sealed class LayeredSpecSource(ISpecSource overlay, ISpecSource beneath) : ISpecSource
{
    public SpecRegistryEntry? Find(string name) => overlay.Find(name) ?? beneath.Find(name);

    // Collections are registered in compiled code and have no runtime counterpart.
    public CollectionBinding<TParent>? FindCollection<TParent>(string path) =>
        beneath.FindCollection<TParent>(path);
}
