using Shouldly;
using Xunit;

namespace Motiv.Serialization.Tests.Printing;

/// <summary>The model and specs the corpus is written against. Public: the printed C# is compiled against this assembly.</summary>
public sealed record PrinterCustomer(bool IsActive, int Age, int OrderCount, IReadOnlyList<int> Orders);

/// <summary>The compiled specs every corpus document resolves through.</summary>
public static class PrinterSpecs
{
    public static SpecBase<PrinterCustomer, string> IsActive { get; } =
        Spec.Build((PrinterCustomer c) => c.IsActive).WhenTrue("customer is active").WhenFalse("customer is inactive").Create();
    public static SpecBase<PrinterCustomer, string> IsAdult { get; } =
        Spec.Build((PrinterCustomer c) => c.Age >= 18).WhenTrue("adult").WhenFalse("minor").Create();
    public static SpecBase<PrinterCustomer, string> HasOrders { get; } =
        Spec.Build((PrinterCustomer c) => c.OrderCount > 0).WhenTrue("has orders").WhenFalse("no orders").Create();
    public static SpecBase<int, string> IsPositive { get; } =
        Spec.Build((int n) => n > 0).WhenTrue("positive").WhenFalse("not positive").Create();

    public static SpecRegistry Registry() => new SpecRegistry()
        .Register("customer.is-active", IsActive)
        .Register("customer.is-adult", IsAdult)
        .Register("customer.has-orders", HasOrders)
        .Register("is-positive", IsPositive)
        .RegisterParameterised<PrinterCustomer>(
            "customer.min-orders",
            [new RuleParameterDeclaration("n", RuleParameterType.Integer, false, null)],
            args => Spec.Build((PrinterCustomer c) => c.OrderCount >= (int)args["n"]!)
                .WhenTrue($"at least {args["n"]} orders").WhenFalse($"fewer than {args["n"]} orders").Create())
        .RegisterCollection<PrinterCustomer, int>("orders", c => c.Orders);
}

#if !NETFRAMEWORK
/// <summary>
/// The printer's contract: over a corpus that exercises every node kind, the printed C# compiles
/// and, evaluated over a scenario set, decides and explains exactly as the bound document does. A
/// document feature that lands without a printer case fails here.
/// </summary>
public class PrinterFidelityTests
{
    private const string PrintedNamespace = "Motiv.Serialization.Tests.Printed";

    private static readonly PrinterCustomer[] Scenarios =
    [
        new(true, 30, 3, [1, 2, 3]),
        new(true, 16, 1, [5]),
        new(false, 41, 12, [-1, 4]),
        new(true, 25, 0, []),
        new(false, 17, 2, [0, 0]),
        new(true, 70, 2, [-3, -4]),
    ];

    public static TheoryData<string> Documents =>
        [.. typeof(PrinterFidelityTests).Assembly.GetManifestResourceNames()
            .Where(name => name.Contains(".Printing.Corpus.", StringComparison.Ordinal))
            .Select(name => name.Substring(name.IndexOf(".Corpus.", StringComparison.Ordinal) + 8))
            .OrderBy(name => name, StringComparer.Ordinal)];

    [Theory]
    [MemberData(nameof(Documents))]
    public void Should_print_c_sharp_that_decides_and_explains_as_the_document_does(string file)
    {
        // Arrange — the document bound, and its print compiled
        var json = Read(file);
        var registry = PrinterSpecs.Registry();
        var bound = new RuleSerializer(registry).Deserialize<PrinterCustomer>(json);
        var printed = CSharpPrinter.Print(json, new CSharpPrintOptions
        {
            ModelType = typeof(PrinterCustomer),
            ClassName = "Printed",
            Namespace = PrintedNamespace,
            Collections = new Dictionary<string, CSharpCollectionHandle> { ["orders"] = new("int", "c => c.Orders") },
        });
        printed.Warnings.ShouldBeEmpty(file);
        var type = PrintedRuleCompiler.Compile(printed.Source, PrintedNamespace, "Printed");
        var compiled = (SpecBase<PrinterCustomer, string>)PrintedRuleCompiler.Invoke(type, "Build", registry);

        // Act & Assert — every scenario decides and explains the same
        foreach (var customer in Scenarios)
        {
            var expected = bound.Evaluate(customer);
            var actual = compiled.Evaluate(customer);
            actual.Satisfied.ShouldBe(expected.Satisfied, $"{file}: {customer}");
            actual.Assertions.ShouldBe(expected.Assertions, ignoreOrder: true, $"{file}: {customer}");
            actual.Justification.ShouldBe(expected.Justification, $"{file}: {customer}");
        }
    }

    private static string Read(string file)
    {
        var resource = typeof(PrinterFidelityTests).Assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith(".Corpus." + file, StringComparison.Ordinal));
        using var stream = typeof(PrinterFidelityTests).Assembly.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
#endif
