using Motiv.Serialization;

namespace Motiv.Serialization.Tests;

public class StoreGenerationTests
{
    [Fact]
    public void Should_report_movement_when_either_component_moves()
    {
        // Arrange
        var origin = new StoreGeneration(1, 1);

        // Act & Assert — either store is enough to make a replica stale
        new StoreGeneration(2, 1).MovedFrom(origin).ShouldBeTrue();
        new StoreGeneration(1, 2).MovedFrom(origin).ShouldBeTrue();
        new StoreGeneration(1, 1).MovedFrom(origin).ShouldBeFalse();
    }

    [Fact]
    public void Should_report_being_behind_when_any_component_is_lower()
    {
        // Arrange — deliberately mixed: newer rules, older propositions
        var observed = new StoreGeneration(5, 2);
        var highest = new StoreGeneration(4, 3);

        // Act & Assert — the two sequences are independent, so "behind" is component-wise.
        // There is no total order to appeal to and inventing one would be a fiction.
        observed.IsBehind(highest).ShouldBeTrue();
        highest.IsBehind(observed).ShouldBeTrue();
        observed.IsBehind(observed).ShouldBeFalse();
    }

    [Fact]
    public void Should_round_trip_through_its_wire_token()
    {
        // Arrange
        var generation = new StoreGeneration(12, 7);

        // Act
        var parsed = StoreGeneration.TryParseToken(generation.ToToken(), out var result);

        // Assert
        parsed.ShouldBeTrue();
        result.ShouldBe(generation);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("nonsense")]
    [InlineData("r1")]
    [InlineData("r1.pX")]
    public void Should_refuse_a_token_it_did_not_write(string? token)
    {
        // Act & Assert — a header is caller-supplied text, never trusted input
        StoreGeneration.TryParseToken(token, out _).ShouldBeFalse();
    }
}
