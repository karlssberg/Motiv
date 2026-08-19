using Motiv.Serialization;
using Motiv.Serialization.Testing;

namespace Motiv.Serialization.EntityFrameworkCore.Tests;

/// <summary>
/// The EF Core store against the same contract the in-memory store passes. This class is the point
/// of the conformance suite: the claim that a test written against InMemoryRuleStore holds against a
/// database is checked here rather than asserted in a comment.
/// </summary>
public class EfRuleStoreTests : RuleStoreConformance
{
    private SqliteStoreFixture _fixture = null!;

    protected override async Task<IRuleStore> CreateStoreAsync()
    {
        _fixture = await SqliteStoreFixture.CreateAsync();
        return new EfRuleStore(_fixture.Factory);
    }

    protected override async Task DisposeStoreAsync() => await _fixture.DisposeAsync();
}
