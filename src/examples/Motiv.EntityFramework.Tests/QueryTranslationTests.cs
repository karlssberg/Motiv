using System.Linq.Expressions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Shouldly;

namespace Motiv.EntityFramework.Tests;

public class QueryTranslationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly CustomerDbContext _context;

    public QueryTranslationTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<CustomerDbContext>()
            .UseSqlite(_connection)
            .Options;
        _context = new CustomerDbContext(options);
        _context.Database.EnsureCreated();
        _context.Customers.AddRange(
            new Customer { Id = 1, Age = 17, IsActive = true },
            new Customer { Id = 2, Age = 30, IsActive = true },
            new Customer { Id = 3, Age = 45, IsActive = false },
            new Customer { Id = 4, Age = 70, IsActive = true });
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public void Should_translate_a_composed_spec_to_a_server_side_where_clause()
    {
        // Arrange
        var isAdult = Spec.From((Customer c) => c.Age >= 18).Create("is adult");
        var isActive = Spec.From((Customer c) => c.IsActive).Create("is active");
        var eligible = isAdult & isActive;

        // Act
        var query = _context.Customers.Where(eligible);
        var sql = query.ToQueryString();
        var act = query.Select(c => c.Id).OrderBy(id => id).ToArray();

        // Assert — a WHERE clause in the generated SQL proves server-side translation
        sql.ShouldContain("WHERE");
        act.ShouldBe([2, 4]);
    }

    [Fact]
    public void Should_translate_a_negated_or_else_composition()
    {
        // Arrange
        var isSenior = Spec.From((Customer c) => c.Age >= 65).Create("is senior");
        var isInactive = Spec.From((Customer c) => !c.IsActive).Create("is inactive");
        var needsReview = isSenior.OrElse(isInactive);
        var fine = !needsReview;

        // Act
        var act = _context.Customers.Where(fine).Select(c => c.Id).OrderBy(id => id).ToArray();

        // Assert
        act.ShouldBe([1, 2]);
    }

    [Fact]
    public void Should_translate_expressions_recovered_from_decomposed_causes()
    {
        // Arrange — recover the atomic cause expression and re-query with it
        Expression<Func<Customer, bool>> expression = c => c.Age >= 18 & c.IsActive;
        var spec = expression.ToSpec();
        var binary = (Motiv.Traversal.IBinaryOperationSpec<Customer, string>)spec;
        var ageAtom = (IExpressionSpec<Customer>)binary.Left;

        // Act
        var act = _context.Customers.Where(ageAtom.ToExpression()).Select(c => c.Id).OrderBy(id => id).ToArray();

        // Assert
        act.ShouldBe([2, 3, 4]);
    }
}
