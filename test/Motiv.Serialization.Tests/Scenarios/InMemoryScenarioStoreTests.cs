using Motiv.Serialization;
using Motiv.Serialization.Testing;

namespace Motiv.Serialization.Tests.Scenarios;

/// <summary>The in-memory scenario store against the shared store contract.</summary>
public class InMemoryScenarioStoreTests : ScenarioStoreConformance
{
    protected override Task<IScenarioStore> CreateStoreAsync() =>
        Task.FromResult<IScenarioStore>(new InMemoryScenarioStore());
}
