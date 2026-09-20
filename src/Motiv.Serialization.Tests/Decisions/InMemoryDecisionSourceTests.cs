using Motiv.Serialization.Testing;

namespace Motiv.Serialization.Tests.Decisions;

/// <summary>The in-memory sink read back: the reference implementation of <see cref="IDecisionSource"/>.</summary>
public class InMemoryDecisionSourceTests : DecisionSourceConformance
{
    protected override Task<(IDecisionSink Sink, IDecisionSource Source)> CreateAsync()
    {
        var sink = new InMemoryDecisionSink();
        return Task.FromResult<(IDecisionSink, IDecisionSource)>((sink, sink));
    }
}
