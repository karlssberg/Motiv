using Shouldly;

namespace Motiv.CodeFix.Tests;

/// <summary>
///     Generic declarations whose type parameters the spec must declare, with the constraints they carry.
/// </summary>
public class MotivConvertToSpecGenericTests
{
    private static readonly Dictionary<string, string> Sources = new()
    {
        ["OverrideInheritingConstraints"] =
            """
            using System;

            namespace MyNamespace;

            public abstract class Base
            {
                public abstract bool IsBetween<T>(T value, T min) where T : IComparable<T>;
            }

            public class Derived : Base
            {
                public override bool IsBetween<T>(T value, T min) => [|value.CompareTo(min) > 0 && min.CompareTo(value) < 0|];
            }
            """,
        ["PartialTypeRepeatingConstraints"] =
            """
            using System;

            namespace MyNamespace;

            public partial class Box<T> where T : IComparable<T>;

            public partial class Box<T> where T : IComparable<T>
            {
                public bool IsAbove(T value, T min) => [|value.CompareTo(min) > 0 && min.CompareTo(value) < 0|];
            }
            """,
        ["ConstraintNamingAnotherTypeParameter"] =
            """
            using System.Collections.Generic;

            namespace MyNamespace;

            public static class Collections
            {
                public static bool HasAny<TItem, TList>(TList list) where TList : ICollection<TItem> =>
                    [|list is not null && list.Count > 0|];
            }
            """,
        ["NestedTypeOfGenericClass"] =
            """
            namespace MyNamespace;

            public class Tree<T>
            {
                public class Node
                {
                    public int Count;
                }

                public bool AreBothFilled(Node left, Node right) => [|left.Count > 0 && right.Count > 0|];
            }
            """,
        ["EveryConstraintKind"] =
            """
            namespace MyNamespace;

            public static class Checks
            {
                public static bool AreSet<TValue, TItem, TKey, TRaw>(TValue value, TItem item, TKey key, TRaw raw)
                    where TValue : struct
                    where TItem : class, new()
                    where TKey : notnull
                    where TRaw : unmanaged =>
                    [|!value.Equals(default(TValue)) && item is not null && key is not null && !raw.Equals(default(TRaw))|];
            }
            """,
        ["ConstraintTypeQualifiedInSource"] =
            """
            namespace MyNamespace;

            public static class Streams
            {
                public static bool AreReadable<T>(T first, T second) where T : System.IO.Stream =>
                    [|first.CanRead && second.CanRead|];
            }
            """
    };

    [Theory]
    [InlineData("OverrideInheritingConstraints")]
    [InlineData("PartialTypeRepeatingConstraints")]
    [InlineData("ConstraintNamingAnotherTypeParameter")]
    [InlineData("NestedTypeOfGenericClass")]
    [InlineData("EveryConstraintKind")]
    [InlineData("ConstraintTypeQualifiedInSource")]
    public async Task Should_declare_every_type_parameter_the_spec_depends_on_with_its_constraints(string source)
    {
        var outcome = await CodeFixHarness.ApplyFix(Sources[source]);

        outcome.CompilerErrors.ShouldBeEmpty(outcome.FixedSource);
    }

    [Fact]
    public async Task Should_keep_a_nullable_reference_type_constraint_nullable()
    {
        const string source =
            """
            #nullable enable
            namespace MyNamespace;

            public static class Checks
            {
                public static bool AreBothSet<T>(T? first, T? second) where T : class? =>
                    [|first is not null && second is not null|];
            }
            """;

        var outcome = await CodeFixHarness.ApplyFix(source);

        // Once on the method, once on the spec
        outcome.FixedSource.Split(["where T : class?"], StringSplitOptions.None).Length.ShouldBe(3, outcome.FixedSource);
    }
}
