using Motiv.Serialization;

namespace Motiv.Serialization.Tests.Propositions;

public class ScopeGenerationTests
{
    [Fact]
    public void Should_publish_a_mutation_as_one_new_generation()
    {
        // Arrange
        var scope = new BindingScope(new SpecRegistry());
        var before = scope.Current;

        // Act
        scope.Mutate(builder => builder.SetSequence(new StoreGeneration(1, 0)));

        // Assert — the old generation is untouched, not edited
        scope.Current.ShouldNotBeSameAs(before);
        before.Sequence.ShouldBe(StoreGeneration.Zero);
        scope.Current.Sequence.ShouldBe(new StoreGeneration(1, 0));
    }

    [Fact]
    public void Should_move_the_write_stamp_on_every_mutation()
    {
        // Arrange
        var scope = new BindingScope(new SpecRegistry());
        var before = scope.WriteStamp;

        // Act
        scope.Mutate(builder => builder.SetSequence(new StoreGeneration(1, 0)));

        // Assert — this is what a refresh compares against to know a publish beat it
        scope.WriteStamp.ShouldNotBe(before);
    }

    [Fact]
    public void Should_refuse_a_swap_whose_write_stamp_is_stale()
    {
        // Arrange
        var scope = new BindingScope(new SpecRegistry());
        var stamp = scope.WriteStamp;
        var successor = scope.Current;

        // Act — a publish lands after the successor was built
        scope.Mutate(builder => builder.SetSequence(new StoreGeneration(9, 9)));
        var swapped = scope.TrySwap(successor, stamp);

        // Assert — the rebuild is discarded, and the publish survives
        swapped.ShouldBeFalse();
        scope.Current.Sequence.ShouldBe(new StoreGeneration(9, 9));
    }

    [Fact]
    public void Should_refuse_to_build_a_generation_twice()
    {
        // Arrange — Build hands the generation the builder's own collections, so a second Build would
        // hand a second world the same ones
        var scope = new BindingScope(new SpecRegistry());
        var builder = new ScopeGenerationBuilder(scope.Registry, scope.Current);
        builder.Build();

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void Should_refuse_a_write_after_the_generation_was_built()
    {
        // Arrange
        var scope = new BindingScope(new SpecRegistry());
        var builder = new ScopeGenerationBuilder(scope.Registry, scope.Current);
        scope.TrySwap(builder.Build(), scope.WriteStamp);

        // Act & Assert — this write would edit a world other threads are already reading
        Should.Throw<InvalidOperationException>(() => builder.SetSequence(new StoreGeneration(1, 1)));
        Should.Throw<InvalidOperationException>(() => builder.Graph);
    }

    [Fact]
    public void Should_accept_a_swap_whose_write_stamp_still_holds()
    {
        // Arrange
        var scope = new BindingScope(new SpecRegistry());
        var stamp = scope.WriteStamp;
        var builder = new ScopeGenerationBuilder(scope.Registry, scope.Current);
        builder.SetSequence(new StoreGeneration(4, 4));

        // Act
        var swapped = scope.TrySwap(builder.Build(), stamp);

        // Assert
        swapped.ShouldBeTrue();
        scope.Current.Sequence.ShouldBe(new StoreGeneration(4, 4));
    }
}
