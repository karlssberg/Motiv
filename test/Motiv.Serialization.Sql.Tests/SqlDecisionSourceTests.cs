using Motiv.Serialization.Testing;

namespace Motiv.Serialization.Sql.Tests;

/// <summary>The SQL sink read back through <see cref="IDecisionSource"/>, on SQLite.</summary>
public class SqlDecisionSourceTests : DecisionSourceConformance
{
    private SqliteDecisionFixture _fixture = null!;
    private SqlDecisionSink _sink = null!;

    protected override Task<(IDecisionSink Sink, IDecisionSource Source)> CreateAsync()
    {
        _fixture = SqliteDecisionFixture.Create();
        _sink = _fixture.Sink();
        return Task.FromResult<(IDecisionSink, IDecisionSource)>((_sink, _sink));
    }

    protected override async Task DisposeStoreAsync()
    {
        await _sink.DisposeAsync();
        await _fixture.DisposeAsync();
    }
}
