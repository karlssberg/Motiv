using Motiv.Serialization;
using Motiv.Serialization.Testing;

namespace Motiv.RulesEngine.Sample.Tests;

/// <summary>
/// The sample's file-backed rule store against the same contract the in-memory and EF stores pass.
/// A third implementation is what shows the suite describes the contract rather than any one store.
/// </summary>
public class JsonFileRuleStoreConformanceTests : RuleStoreConformance
{
    private string _path = string.Empty;

    protected override Task<IRuleStore> CreateStoreAsync()
    {
        _path = Path.Combine(Path.GetTempPath(), $"rules-{Guid.NewGuid():N}.json");
        return Task.FromResult<IRuleStore>(new JsonFileRuleStore(_path));
    }

    protected override Task DisposeStoreAsync()
    {
        if (File.Exists(_path))
            File.Delete(_path);
        return Task.CompletedTask;
    }
}
