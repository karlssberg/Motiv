using System.Data.Common;
using Shouldly;
using Xunit;

namespace Motiv.Serialization.Sql.Tests;

/// <summary>
/// What the sink refuses to be built without. <c>IDecisionSink</c>'s contract asks implementations to
/// "fail fast at construction" rather than throw on the writer loop, and a decision sink has two
/// things it genuinely cannot choose for the adopter: how long to keep a record, and which SQL to
/// speak. Both are absent by default and refused here.
/// </summary>
public class SqlDecisionSinkConstructionTests
{
    private static Func<DbConnection> AnyFactory => () => throw new NotSupportedException();

    [Fact]
    public void Should_refuse_a_sink_with_no_retention_window()
    {
        // Arrange — a dialect, but no window
        var options = new SqlDecisionSinkOptions { Dialect = DecisionSqlDialect.Sqlite };

        // Act
        var act = () => new SqlDecisionSink(AnyFactory, options);

        // Assert — and the message must name the property, since this is the only signal an adopter
        // gets that the log they just wired up would otherwise grow forever
        var exception = Should.Throw<ArgumentException>(act);
        exception.Message.ShouldContain(nameof(SqlDecisionSinkOptions.Retention));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Should_refuse_a_retention_window_that_is_not_positive(int days)
    {
        // Arrange
        var options = new SqlDecisionSinkOptions();

        // Act
        var act = () => { options.Retention = TimeSpan.FromDays(days); };

        // Assert — a zero window would satisfy "a window was set" while deleting everything written
        Should.Throw<ArgumentOutOfRangeException>(act);
    }

    [Fact]
    public void Should_refuse_an_infinite_retention_window()
    {
        // Arrange
        var options = new SqlDecisionSinkOptions();

        // Act — the obvious way to spell "keep forever", which is the one thing this must not allow
        var act = () => { options.Retention = Timeout.InfiniteTimeSpan; };

        // Assert
        Should.Throw<ArgumentOutOfRangeException>(act);
    }

    [Fact]
    public void Should_refuse_a_sink_with_no_dialect()
    {
        // Arrange — a window, but nothing saying which SQL to write
        var options = new SqlDecisionSinkOptions { Retention = TimeSpan.FromDays(90) };

        // Act
        var act = () => new SqlDecisionSink(AnyFactory, options);

        // Assert — not defaulted to SQLite: the connection factory says nothing about the engine, so
        // a default would be a guess that fails at the first write rather than at startup
        var exception = Should.Throw<ArgumentException>(act);
        exception.Message.ShouldContain(nameof(SqlDecisionSinkOptions.Dialect));
    }

    [Fact]
    public void Should_refuse_a_null_connection_factory()
    {
        // Arrange
        var options = Options();

        // Act
        var act = () => new SqlDecisionSink(null!, options);

        // Assert
        Should.Throw<ArgumentNullException>(act);
    }

    [Fact]
    public void Should_refuse_null_options()
    {
        // Act
        var act = () => new SqlDecisionSink(AnyFactory, null!);

        // Assert
        Should.Throw<ArgumentNullException>(act);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Should_refuse_a_purge_batch_size_below_one(int size)
    {
        // Arrange
        var options = new SqlDecisionSinkOptions();

        // Act — a batch of zero would loop forever deleting nothing
        var act = () => { options.PurgeBatchSize = size; };

        // Assert
        Should.Throw<ArgumentOutOfRangeException>(act);
    }

    [Fact]
    public void Should_refuse_a_purge_interval_that_is_not_positive()
    {
        // Arrange
        var options = new SqlDecisionSinkOptions();

        // Act
        var act = () => { options.PurgeInterval = TimeSpan.Zero; };

        // Assert
        Should.Throw<ArgumentOutOfRangeException>(act);
    }

    [Fact]
    public async Task Should_accept_a_window_and_a_dialect()
    {
        // Act — the whole contract: name a window, name a dialect, and nothing else is required.
        // A factory that throws proves construction opens no connection: the purge loop waits out
        // its first interval before touching the database, so startup is never the moment a host
        // discovers the log is unreachable.
        await using var sink = new SqlDecisionSink(AnyFactory, Options());

        // Assert
        sink.LastPurgeUtc.ShouldBeNull();
    }

    private static SqlDecisionSinkOptions Options() => new()
    {
        Dialect = DecisionSqlDialect.Sqlite,
        Retention = TimeSpan.FromDays(90)
    };
}
