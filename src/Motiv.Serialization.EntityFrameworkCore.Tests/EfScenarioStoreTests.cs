using Motiv.Serialization;
using Motiv.Serialization.Testing;

namespace Motiv.Serialization.EntityFrameworkCore.Tests;

/// <summary>The EF Core scenario store against the shared store contract.</summary>
public class EfScenarioStoreTests : ScenarioStoreConformance
{
    private SqliteStoreFixture _fixture = null!;

    protected override async Task<IScenarioStore> CreateStoreAsync()
    {
        _fixture = await SqliteStoreFixture.CreateAsync();
        return new EfScenarioStore(_fixture.Factory);
    }

    protected override async Task DisposeStoreAsync() => await _fixture.DisposeAsync();
}
