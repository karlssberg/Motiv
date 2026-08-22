using Motiv.Serialization;
using Motiv.Serialization.Testing;

namespace Motiv.Serialization.Tests.Propositions;

/// <summary>The in-memory proposition store against the shared store contract.</summary>
public class InMemoryPropositionStoreTests : PropositionStoreConformance
{
    protected override Task<IPropositionStore> CreateStoreAsync() =>
        Task.FromResult<IPropositionStore>(new InMemoryPropositionStore());
}
