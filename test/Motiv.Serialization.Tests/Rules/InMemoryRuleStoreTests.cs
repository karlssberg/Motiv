using Motiv.Serialization;
using Motiv.Serialization.Testing;

namespace Motiv.Serialization.Tests.Rules;

/// <summary>
/// The in-memory store against the shared store contract. It is the oracle the other
/// implementations are held to, so it must pass the same suite they do.
/// </summary>
public class InMemoryRuleStoreTests : RuleStoreConformance
{
    protected override Task<IRuleStore> CreateStoreAsync() =>
        Task.FromResult<IRuleStore>(new InMemoryRuleStore());
}
