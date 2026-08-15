using Motiv.Serialization;
using Shouldly;
using Xunit;

namespace Motiv.RulesEngine.Sample.Tests;

public class JsonFilePropositionStoreTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"motiv-propositions-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }

    private static StoredProposition Stored(string name, int version = 1) =>
        new(name, "customer", """{ "rule": { "spec": "customer.is-active" } }""", version, "a description");

    [Fact]
    public void Should_report_no_propositions_when_the_file_is_absent()
    {
        // Act & Assert — a first run must not need the file to exist
        new JsonFilePropositionStore(_path).Load().ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_persist_across_instances()
    {
        // Arrange
        await new JsonFilePropositionStore(_path).WriteAsync(PropositionBatch.Save(Stored("customer.a")), default);

        // Act — a second instance stands in for a restart
        var loaded = new JsonFilePropositionStore(_path).Load();

        // Assert
        loaded.Count.ShouldBe(1);
        loaded[0].Name.ShouldBe("customer.a");
        loaded[0].Description.ShouldBe<string?>("a description");
    }

    [Fact]
    public async Task Should_replace_a_proposition_of_the_same_name()
    {
        // Arrange
        var store = new JsonFilePropositionStore(_path);
        await store.WriteAsync(PropositionBatch.Save(Stored("customer.a", version: 1)), default);

        // Act
        await store.WriteAsync(PropositionBatch.Save(Stored("customer.a", version: 2)), default);

        // Assert
        var loaded = new JsonFilePropositionStore(_path).Load();
        loaded.Count.ShouldBe(1);
        loaded[0].Version.ShouldBe(2);
    }

    [Fact]
    public async Task Should_delete_a_proposition()
    {
        // Arrange
        var store = new JsonFilePropositionStore(_path);
        await store.WriteAsync(PropositionBatch.Save(Stored("customer.a")), default);
        await store.WriteAsync(PropositionBatch.Save(Stored("customer.b")), default);

        // Act
        await store.WriteAsync(PropositionBatch.Delete("customer.a"), default);

        // Assert
        new JsonFilePropositionStore(_path).Load()
            .Select(proposition => proposition.Name).ShouldBe(["customer.b"]);
    }

    [Fact]
    public async Task Should_write_a_save_and_a_delete_in_one_batch()
    {
        // Arrange — an envelope's writes must land in a single file rewrite
        var store = new JsonFilePropositionStore(_path);
        await store.WriteAsync(PropositionBatch.Save(Stored("customer.a")), default);

        // Act
        await store.WriteAsync(new PropositionBatch([Stored("customer.b")], ["customer.a"]), default);

        // Assert
        new JsonFilePropositionStore(_path).Load()
            .Select(proposition => proposition.Name).ShouldBe(["customer.b"]);
    }

    [Fact]
    public void Should_treat_a_malformed_file_as_empty_rather_than_throwing()
    {
        // Arrange — a hand-edited file must not stop the sample booting
        File.WriteAllText(_path, "{ not json");

        // Act
        var load = () => new JsonFilePropositionStore(_path).Load();

        // Assert
        load.ShouldNotThrow().ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_move_the_generation_forward_after_a_write()
    {
        // Arrange
        var store = new JsonFilePropositionStore(_path);
        await store.WriteAsync(PropositionBatch.Save(Stored("customer.a")), default);
        var generation = await store.GetGenerationAsync(default);

        // Act
        await store.WriteAsync(PropositionBatch.Save(Stored("customer.b")), default);

        // Assert
        (await store.GetGenerationAsync(default)).ShouldBeGreaterThan(generation);
    }

    [Fact]
    public async Task Should_move_the_generation_forward_across_instances()
    {
        // Arrange
        var store = new JsonFilePropositionStore(_path);
        await store.WriteAsync(PropositionBatch.Save(Stored("customer.a")), default);
        var generation = await store.GetGenerationAsync(default);

        // Act
        await new JsonFilePropositionStore(_path).WriteAsync(PropositionBatch.Save(Stored("customer.b")), default);

        // Assert — the fencing token must be derived from the file, not from instance state
        (await new JsonFilePropositionStore(_path).GetGenerationAsync(default))
            .ShouldBeGreaterThan(generation);
    }

    [Fact]
    public void Should_treat_a_file_it_cannot_read_as_empty_rather_than_throwing()
    {
        // Arrange — a permission-denied state file must not stop the host booting. Unix-only:
        // Windows permissions are not expressible this way and the guard skips rather than fails.
        if (OperatingSystem.IsWindows()) return;

        File.WriteAllText(_path, "[]");
        File.SetUnixFileMode(_path, UnixFileMode.None);

        try
        {
            // Act
            var load = () => new JsonFilePropositionStore(_path).Load();

            // Assert
            load.ShouldNotThrow().ShouldBeEmpty();
        }
        finally
        {
            // Restore so Dispose can delete it.
            File.SetUnixFileMode(_path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }
}
