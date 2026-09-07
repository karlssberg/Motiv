using System.Reflection;
using Motiv.HigherOrderProposition;

namespace Motiv.Tests.Traversal;

/// <summary>
/// The list of seams that declare themselves excluded from
/// <see cref="MotivLimits.MaxEvaluationSize" /> is maintained by hand, and
/// <see href="https://github.com/karlssberg/Motiv/pull/206">#206</see> shipped claiming there were two
/// of them when there were three — found by reading, with every job green. This is that claim made
/// checkable for the one seam with nineteen copies of itself: <b>every higher-order proposition reaches
/// its decision through <c>HigherOrderResults.MaterializeAndDecide</c></b>, so the predicate a caller
/// supplies through <c>As(...)</c> is not charged to the composition
/// (<see href="https://github.com/karlssberg/Motiv/issues/208">#208</see>).
/// </summary>
/// <remarks>
/// Written against the <b>IL</b> rather than the source, so it cannot be satisfied by a comment, and
/// with its population <b>discovered</b> rather than listed, so a twentieth family is in scope the
/// moment it is written. A hard-coded list of nineteen type names would pass green on precisely the
/// change this exists to catch.
/// <para>
/// Deliberately not in <see cref="HigherOrderPredicateBudgetTests" />: that class mutates
/// <see cref="MotivLimits.MaxEvaluationSize" /> and so runs in a serialized collection, and a
/// whole-assembly reflection sweep needs neither.
/// </para>
/// </remarks>
public class HigherOrderSeamGateTests
{
    /// <summary>
    /// The four families, by namespace. A higher-order proposition is a non-abstract class in one of them
    /// whose name <em>ends</em> with <c>Proposition</c> — a suffix rather than a substring, so that a
    /// helper named <c>PropositionBuilder</c> or <c>PropositionExtensions</c> added to one of these
    /// namespaces later cannot join the population. The convention is exact today, which
    /// <see cref="Should_find_every_higher_order_proposition" /> pins so that a family added elsewhere
    /// shows up as a miscount rather than as a silent omission.
    /// <para>
    /// The suffix is tested against the name with its <b>arity stripped</b>. All nineteen are generic, and
    /// a generic type reflects as <c>HigherOrderFromBooleanPredicateProposition`2</c> — so a plain
    /// <c>EndsWith("Proposition")</c> matches none of them and empties the population. It was the count
    /// case below that said so, which is the reason it is a separate assertion rather than a guard clause
    /// inside the gate.
    /// </para>
    /// </summary>
    private static readonly string[] Families =
    [
        "Motiv.HigherOrderProposition.BooleanPredicate",
        "Motiv.HigherOrderProposition.BooleanResultPredicate",
        "Motiv.HigherOrderProposition.PolicyResultPredicate",
        "Motiv.HigherOrderProposition.ExpressionTree"
    ];

    private const int KnownFamilyCount = 19;

    /// <summary>
    /// The result classes: every higher-order proposition builds one, and every one of them defers a cause
    /// selector — sixteen of them a value delegate as well. Their count is pinned for the same reason the
    /// propositions' is.
    /// </summary>
    private const int KnownResultCount = 19;

    private const int KnownValueDelegateResultCount = 16;

    /// <summary>Of those sixteen, the eight whose delegates yield and so must take <c>ResolveValues</c>.</summary>
    private const int KnownYieldingResultCount = 8;

    private static Type[] HigherOrderPropositions() => FamilyTypesEndingWith("Proposition");

    /// <summary>
    /// The result population, discovered the same way as the propositions': a non-abstract class in one of
    /// the four family namespaces whose arity-stripped name ends with <c>Result</c>.
    /// </summary>
    private static Type[] HigherOrderResultTypes() => FamilyTypesEndingWith("Result");

    /// <summary>
    /// Every non-abstract class in one of the four family namespaces whose arity-stripped name ends with
    /// <paramref name="suffix" />.
    /// </summary>
    private static Type[] FamilyTypesEndingWith(string suffix) =>
        typeof(Spec).Assembly
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false }
                           && NameWithoutArity(type).EndsWith(suffix, StringComparison.Ordinal)
                           && Families.Contains(type.Namespace))
            .ToArray();

    /// <summary>
    ///     A generic type's <see cref="Type.Name" /> carries its arity — <c>Foo`2</c> — so the suffix has to be
    ///     tested against the part before the backtick.
    /// </summary>
    private static string NameWithoutArity(Type type)
    {
        var arity = type.Name.IndexOf('`');
        return arity < 0 ? type.Name : type.Name.Substring(0, arity);
    }

    private static MethodInfo Seam(string name) =>
        typeof(HigherOrderResults)
            .GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)
            .ShouldNotBeNull($"the seam this gate is stated in terms of has to exist: {name}");

    /// <summary>
    /// Whether the type defers a caller's cause selector, read off the <em>shape</em> of a constructor
    /// parameter — <c>Func&lt;bool, IEnumerable&lt;T&gt;, IEnumerable&lt;T&gt;&gt;</c> — rather than off a
    /// parameter name. One of these families spells its value delegates <c>trueBecause</c> where the rest
    /// spell them <c>whenTrue</c>, which is exactly the kind of drift a name-matched population absorbs
    /// silently.
    /// </summary>
    private static bool DefersACauseSelector(Type type) =>
        HasConstructorParameter(type, func => func.GetGenericTypeDefinition() == typeof(Func<,,>)
                                              && func.GetGenericArguments()[0] == typeof(bool));

    /// <summary>
    /// Whether the type defers a caller's <c>WhenTrue</c>/<c>WhenFalse</c>, again by shape: a one-argument
    /// delegate over one of the three <c>HigherOrder…Evaluation</c> types.
    /// </summary>
    private static bool DefersAValueDelegate(Type type) => DefersAValueDelegate(type, yielding: false)
                                                           || DefersAValueDelegate(type, yielding: true);

    /// <summary>
    /// The same shape, split by whether the delegate <em>yields</em> — its return type is a constructed
    /// <see cref="IEnumerable{T}" /> — because which of the two value seams a result must take depends on
    /// exactly that, and nothing else can tell them apart.
    /// </summary>
    /// <remarks>
    /// <b>The split is the point, not a refinement of it.</b> <c>ResolveValue</c> and <c>ResolveValues</c>
    /// have different names but overlapping signatures: a yielding delegate binds to <c>ResolveValue</c>
    /// perfectly well, with <c>TValue</c> inferred as <c>IEnumerable&lt;string&gt;</c>. The exclusion scope
    /// then closes over the <em>invocation</em>, which for an iterator block runs none of the caller's
    /// code — and the body runs later, at enumeration, outside it. That is #213 reopened, and a gate that
    /// accepted either seam would report success over it.
    /// <para>
    /// A single-valued delegate returns the class's own open <c>TMetadata</c>, or <c>string</c>; neither is
    /// a constructed <see cref="IEnumerable{T}" />, so the two populations do not overlap.
    /// </para>
    /// </remarks>
    private static bool DefersAValueDelegate(Type type, bool yielding) =>
        HasConstructorParameter(type, func => func.GetGenericTypeDefinition() == typeof(Func<,>)
                                              && NameWithoutArity(func.GetGenericArguments()[0])
                                                  .EndsWith("Evaluation", StringComparison.Ordinal)
                                              && Yields(func.GetGenericArguments()[1]) == yielding);

    private static bool Yields(Type returnType) =>
        returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(IEnumerable<>);

    /// <summary>
    /// Whether any constructor of <paramref name="type" /> takes a parameter matching
    /// <paramref name="shape" />. Only generic parameter types are offered to it, so a shape may call
    /// <see cref="Type.GetGenericTypeDefinition" /> without guarding first.
    /// </summary>
    private static bool HasConstructorParameter(Type type, Func<Type, bool> shape) =>
        type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .SelectMany(constructor => constructor.GetParameters())
            .Any(parameter => parameter.ParameterType is { IsGenericType: true } func && shape(func));

    /// <summary>
    /// The population, pinned. A reflection query that quietly stops matching is a gate reporting a
    /// property it is not checking, and everything downstream reads its green as evidence — so the count
    /// is asserted before anything is asserted about its members. Changing this number is how a new
    /// family announces itself.
    /// </summary>
    [Fact]
    public void Should_find_every_higher_order_proposition() =>
        HigherOrderPropositions().Length.ShouldBe(
            KnownFamilyCount,
            "the gate below is only as good as the population it sweeps");

    /// <summary>
    /// The gate itself. Stated over <em>types</em> rather than over methods named <c>EvaluateModels</c>:
    /// the claim is about propositions, and a family that inlined its decision into <c>EvaluatePolicy</c>
    /// or named the method something else would add nothing to a name-derived population and leave this
    /// green.
    /// </summary>
    [Fact]
    public void Should_decide_every_higher_order_proposition_through_the_declared_seam()
    {
        var seam = Seam("MaterializeAndDecide");

        var undeclared = HigherOrderPropositions()
            .Where(type => !DeclaredMethods(type).Any(method => Calls(method, seam)))
            .Select(type => type.Name)
            .OrderBy(name => name)
            .ToArray();

        undeclared.ShouldBeEmpty(
            "a higher-order proposition that applies its predicate outside the seam charges the " +
            "caller's predicate to the enclosing composition");
    }

    /// <summary>
    /// The negative control, without which nothing here distinguishes <em>the scanner found the call in
    /// all nineteen</em> from <em>the scanner returns true for anything</em>. A scanner broken to answer
    /// <c>true</c> unconditionally leaves the gate above green and this case red.
    /// </summary>
    /// <remarks>
    /// <c>HigherOrderShortCircuit.Evaluate</c> is the pointed choice: it is the sibling seam — the
    /// allocation-free funnel to the same elements — so it opens an exclusion of its own and calls
    /// nothing here. A control that were merely unrelated would be a weaker one.
    /// </remarks>
    [Fact]
    public void Should_not_report_a_call_that_is_not_there()
    {
        var control = typeof(HigherOrderShortCircuit)
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            .Where(method => method.Name == "Evaluate" && method.GetMethodBody() is not null)
            .ToArray();

        control.ShouldNotBeEmpty("the control has to be a method that exists and has a body");
        control.ShouldAllBe(method => !Calls(method, Seam("MaterializeAndDecide")));
    }

    /// <summary>
    /// The result population, pinned, and the two properties it is stated over. Nineteen result classes,
    /// every one of them deferring a cause selector; sixteen of them a value delegate as well.
    /// </summary>
    [Fact]
    public void Should_find_every_higher_order_result()
    {
        var results = HigherOrderResultTypes();

        results.Length.ShouldBe(KnownResultCount, "the gates below are only as good as the population they sweep");
        results.Count(DefersACauseSelector).ShouldBe(
            KnownResultCount,
            "a result whose cause selector stopped matching by shape drops out of the gate below");
        results.Count(DefersAValueDelegate).ShouldBe(
            KnownValueDelegateResultCount,
            "and so does one whose value delegates do");
        results.Count(type => DefersAValueDelegate(type, yielding: true)).ShouldBe(
            KnownYieldingResultCount,
            "the two value seams are gated separately, so their populations are pinned separately");
        results.Count(type => DefersAValueDelegate(type, yielding: false)).ShouldBe(
            KnownValueDelegateResultCount - KnownYieldingResultCount,
            "and the two must still account for every result that defers a value delegate");
    }

    /// <summary>
    /// The lazy half of the same seam. A higher-order result defers its cause selector to first property
    /// read, so it runs on whatever evaluation happens to be in flight then — and, being memoized, on
    /// whichever one <em>got there first</em>
    /// (<see href="https://github.com/karlssberg/Motiv/issues/213">#213</see>).
    /// </summary>
    [Fact]
    public void Should_resolve_every_results_cause_selector_through_the_declared_seam()
    {
        var seam = Seam("ResolveCauses");

        var undeclared = HigherOrderResultTypes()
            .Where(DefersACauseSelector)
            .Where(type => !DeclaredMethods(type).Any(method => Calls(method, seam)))
            .Select(type => type.Name)
            .OrderBy(name => name)
            .ToArray();

        undeclared.ShouldBeEmpty(
            "a result that resolves its cause selector outside the seam charges the caller's " +
            "As(...) predicate to whichever evaluation read the property first");
    }

    /// <summary>
    /// The value delegates, gated per seam rather than per class. A yielding delegate must go through
    /// <c>ResolveValues</c>, which materializes <em>inside</em> the scope; a single-valued one through
    /// <c>ResolveValue</c>. Accepting either would leave the one bug the split exists to prevent
    /// reachable with the gate green — see <see cref="DefersAValueDelegate(Type, bool)" />.
    /// </summary>
    [Theory]
    [InlineData(true, "ResolveValues")]
    [InlineData(false, "ResolveValue")]
    public void Should_resolve_every_results_value_delegate_through_the_seam_its_shape_requires(
        bool yielding,
        string seamName)
    {
        var seam = Seam(seamName);

        var undeclared = HigherOrderResultTypes()
            .Where(type => DefersAValueDelegate(type, yielding))
            .Where(type => !DeclaredMethods(type).Any(method => Calls(method, seam)))
            .Select(type => type.Name)
            .OrderBy(name => name)
            .ToArray();

        undeclared.ShouldBeEmpty(
            $"a result whose WhenTrue/WhenFalse {(yielding ? "yields" : "returns one value")} must take " +
            $"{seamName}, or explanation rendering runs outside the exclusion that was written for it");
    }

    private static IEnumerable<MethodInfo> DeclaredMethods(Type type) =>
        type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static
                        | BindingFlags.DeclaredOnly);

    /// <summary>
    /// Whether <paramref name="method" /> calls <paramref name="target" />, by scanning its IL for a
    /// <c>call</c> whose operand token resolves to that method.
    /// </summary>
    /// <remarks>
    /// Deliberately naive: it does not decode operand lengths, so four bytes inside some other
    /// instruction's operand could in principle resolve to the same token. <b>The asymmetry is what
    /// makes that acceptable.</b> The assertion this serves is that a call is <em>present</em>, so an
    /// over-eager scan cannot produce the failure mode that would make the gate lie — a missing call
    /// reported as found would need the seam's own metadata token to appear by accident. If this is ever
    /// wrong it is wrong in the direction that shows up as a red build.
    /// </remarks>
    private static bool Calls(MethodBase method, MethodInfo target)
    {
        var il = method.GetMethodBody()?.GetILAsByteArray();
        if (il is null) return false;

        var module = method.Module;
        var typeArguments = method.DeclaringType?.GetGenericArguments() ?? [];
        var methodArguments = method.IsGenericMethodDefinition ? method.GetGenericArguments() : [];

        for (var i = 0; i + 4 < il.Length; i++)
        {
            if (il[i] != 0x28) continue; // call

            var token = BitConverter.ToInt32(il, i + 1);
            try
            {
                if (Definition(module.ResolveMethod(token, typeArguments, methodArguments)) == target)
                    return true;
            }
            catch (ArgumentException)
            {
                // Not a method token — those four bytes were operand data, not an opcode.
            }
        }

        return false;
    }

    /// <summary>
    /// Reduces a constructed generic method to the definition the gate compares against, so that the
    /// call sites — each closing a seam over its own element type — all resolve to one thing.
    /// </summary>
    private static MethodBase? Definition(MethodBase? called) =>
        called is MethodInfo { IsGenericMethod: true, IsGenericMethodDefinition: false } constructed
            ? constructed.GetGenericMethodDefinition()
            : called;
}
