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

    private static Type[] HigherOrderPropositions() =>
        typeof(Spec).Assembly
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false }
                           && NameWithoutArity(type).EndsWith("Proposition", StringComparison.Ordinal)
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

    private static MethodInfo Seam() =>
        typeof(HigherOrderResults)
            .GetMethod("MaterializeAndDecide", BindingFlags.NonPublic | BindingFlags.Static)
            .ShouldNotBeNull("the seam this gate is stated in terms of has to exist");

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
        var seam = Seam();

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
        control.ShouldAllBe(method => !Calls(method, Seam()));
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
    /// nineteen call sites — each closing the seam over its own element type — all resolve to one thing.
    /// </summary>
    private static MethodBase? Definition(MethodBase? called) =>
        called is MethodInfo { IsGenericMethod: true, IsGenericMethodDefinition: false } constructed
            ? constructed.GetGenericMethodDefinition()
            : called;
}
