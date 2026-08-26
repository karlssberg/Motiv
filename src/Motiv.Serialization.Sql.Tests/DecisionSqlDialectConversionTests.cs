using Microsoft.Data.Sqlite;
using Shouldly;
using Xunit;

namespace Motiv.Serialization.Sql.Tests;

/// <summary>
/// The value conversions, which <see cref="DecisionSqlDialectTests"/> does not reach.
/// </summary>
/// <remarks>
/// <para>
/// Behavioural conformance runs on SQLite, and SQLite overrides every conversion — so the base
/// implementations, which PostgreSQL and SQL Server rely on <em>entirely</em>, are exercised by
/// nothing else in the suite. That is not a coverage nicety: an identity or an instant silently
/// mangled on the way into a parameter is a decision record that cannot be found again, on the two
/// engines an enterprise actually deploys.
/// </para>
/// <para>
/// The reads are checked against a real SQLite reader rather than a stub. What is under test is the
/// base implementation's own behaviour — <c>GetGuid</c>, <c>GetFieldValue&lt;DateTimeOffset&gt;</c> —
/// and a stub asserting that a mock was called would prove only that the test knows what the code
/// says.
/// </para>
/// </remarks>
public class DecisionSqlDialectConversionTests
{
    public static TheoryData<string, DecisionSqlDialect> NativeDialects => new()
    {
        { "PostgreSQL", DecisionSqlDialect.PostgreSql },
        { "SQL Server", DecisionSqlDialect.SqlServer }
    };

    [Theory]
    [MemberData(nameof(NativeDialects))]
    public void Should_bind_an_identity_natively(string name, DecisionSqlDialect dialect)
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act — both engines have a real identity type, so the provider maps the CLR value itself
        var bound = dialect.ToParameter(id);

        // Assert
        bound.ShouldBe(id, customMessage: name);
    }

    [Theory]
    [MemberData(nameof(NativeDialects))]
    public void Should_normalise_an_instant_to_utc_before_binding(string name, DecisionSqlDialect dialect)
    {
        // Arrange — an offset that is not zero, which is what makes this more than a pass-through:
        // Npgsql will not bind a non-UTC DateTimeOffset to timestamptz at all, and the purge's
        // `< @cutoff` compares against rows written the same way
        var instant = new DateTimeOffset(2026, 8, 25, 14, 7, 3, TimeSpan.FromHours(5));

        // Act
        var bound = dialect.ToParameter(instant).ShouldBeOfType<DateTimeOffset>();

        // Assert
        bound.Offset.ShouldBe(TimeSpan.Zero, customMessage: name);
        bound.ShouldBe(instant, customMessage: name);
    }

    [Theory]
    [MemberData(nameof(NativeDialects))]
    public void Should_bind_a_flag_natively(string name, DecisionSqlDialect dialect)
    {
        // Act — boolean and bit, so nothing is converted
        var bound = dialect.ToParameter(true);

        // Assert
        bound.ShouldBe(true, customMessage: name);
    }

    [Fact]
    public void Should_convert_what_sqlite_has_no_type_for()
    {
        // Arrange — SQLite has no identity, instant or boolean type, so all three are spelled out
        // rather than left to a provider's default mapping
        var id = Guid.NewGuid();
        var instant = new DateTimeOffset(2026, 8, 25, 14, 7, 3, TimeSpan.FromHours(5));

        // Act
        var dialect = DecisionSqlDialect.Sqlite;

        // Assert — the instant is a round-trip UTC string, which sorts lexicographically precisely
        // because it is normalised; a mixture of offsets would make the purge's comparison quietly
        // wrong rather than loudly broken
        dialect.ToParameter(id).ShouldBe(id.ToString());
        dialect.ToParameter(instant).ShouldBe("2026-08-25T09:07:03.0000000+00:00");
        dialect.ToParameter(true).ShouldBe(1);
        dialect.ToParameter(false).ShouldBe(0);
    }

    [Theory]
    [MemberData(nameof(NativeDialects))]
    public async Task Should_read_an_identity_and_an_instant_back(string name, DecisionSqlDialect dialect)
    {
        // Arrange — a real reader over values shaped as the native engines store them: SQLite hands
        // back a Guid from GetGuid and a DateTimeOffset from GetFieldValue, which is exactly what the
        // base implementations ask of their own providers
        var id = Guid.NewGuid();
        var instant = new DateTimeOffset(2026, 8, 25, 14, 7, 3, TimeSpan.Zero);

        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT @id, @instant";
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@instant", instant);

        // Act
        await using var reader = await command.ExecuteReaderAsync();
        (await reader.ReadAsync()).ShouldBeTrue();

        // Assert
        dialect.ReadGuid(reader, 0).ShouldBe(id, customMessage: name);
        dialect.ReadTimestamp(reader, 1).ShouldBe(instant, customMessage: name);
    }
}
