using Shouldly;
using Xunit;

namespace Motiv.RulesEngine.Sample.Tests;

public class JsonFileGateStoreTests : IDisposable
{
    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"gate-{Guid.NewGuid():N}.json");

    [Fact]
    public void Should_load_null_when_no_gate_file_exists()
    {
        // Arrange
        var store = new JsonFileGateStore(_path);

        // Act & Assert — absent file is the permissive default
        store.Load().ShouldBeNull();
    }

    [Fact]
    public void Should_round_trip_a_saved_document()
    {
        // Arrange
        var store = new JsonFileGateStore(_path);

        // Act
        store.Save("""{"rule": {"spec": "change.is-rollback"}}""");

        // Assert — a second instance over the same path sees the persisted gate
        var loaded = new JsonFileGateStore(_path).Load();
        loaded.ShouldNotBeNull();
        loaded.ShouldBe("""{"rule": {"spec": "change.is-rollback"}}""");
    }

    [Fact]
    public void Should_overwrite_a_previously_saved_document()
    {
        // Arrange
        var store = new JsonFileGateStore(_path);
        store.Save("""{"rule": {"spec": "change.is-rollback"}}""");

        // Act
        store.Save("""{"rule": {"spec": "change.is-creation"}}""");

        // Assert
        var loaded = store.Load();
        loaded.ShouldNotBeNull();
        loaded.ShouldBe("""{"rule": {"spec": "change.is-creation"}}""");
    }

    [Fact]
    public void Should_delete_the_file_when_saving_null()
    {
        // Arrange
        var store = new JsonFileGateStore(_path);
        store.Save("""{"rule": {"spec": "change.is-rollback"}}""");

        // Act — clearing the gate has exactly one on-disk representation: no file
        store.Save(null);

        // Assert
        File.Exists(_path).ShouldBeFalse();
        store.Load().ShouldBeNull();
    }

    [Fact]
    public void Should_load_null_from_a_blank_file()
    {
        // Arrange — a truncated write must read as "no gate", not as an unbindable document
        File.WriteAllText(_path, "   ");

        // Act & Assert
        new JsonFileGateStore(_path).Load().ShouldBeNull();
    }

    [Fact]
    public void Should_tolerate_saving_null_when_no_file_exists()
    {
        // Arrange
        var store = new JsonFileGateStore(_path);

        // Act & Assert — resetting an already-permissive gate is a no-op, not an error
        Should.NotThrow(() => store.Save(null));
    }

    public void Dispose()
    {
        if (File.Exists(_path))
            File.Delete(_path);
    }
}
