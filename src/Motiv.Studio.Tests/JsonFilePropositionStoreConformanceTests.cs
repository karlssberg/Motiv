using Motiv.Serialization;
using Motiv.Serialization.Testing;

namespace Motiv.Studio.Tests;

/// <summary>Studio's file-backed proposition store against the shared store contract.</summary>
public class JsonFilePropositionStoreConformanceTests : PropositionStoreConformance
{
    private string _path = string.Empty;

    protected override Task<IPropositionStore> CreateStoreAsync()
    {
        _path = Path.Combine(Path.GetTempPath(), $"propositions-{Guid.NewGuid():N}.json");
        return Task.FromResult<IPropositionStore>(new JsonFilePropositionStore(_path));
    }

    protected override Task DisposeStoreAsync()
    {
        if (File.Exists(_path))
            File.Delete(_path);
        return Task.CompletedTask;
    }
}
