using Motiv.Serialization;
using Motiv.Serialization.Testing;

namespace Motiv.Serialization.EntityFrameworkCore.Tests;

/// <summary>The EF Core proposition store against the shared store contract.</summary>
public class EfPropositionStoreTests : PropositionStoreConformance
{
    private SqliteStoreFixture _fixture = null!;

    protected override async Task<IPropositionStore> CreateStoreAsync()
    {
        _fixture = await SqliteStoreFixture.CreateAsync();
        return new EfPropositionStore(_fixture.Factory);
    }

    protected override async Task DisposeStoreAsync() => await _fixture.DisposeAsync();
}
