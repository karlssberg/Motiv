using System.Runtime.CompilerServices;

namespace Motiv.Shared;

/// <summary>
/// Compares results by identity. Needed wherever a result is used as a dictionary key, because
/// <see cref="BooleanResultBase.Equals(BooleanResultBase)" /> compares <c>Satisfied</c> — so a
/// default-keyed dictionary would collapse a whole tree into two entries.
/// </summary>
internal sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T>
    where T : class
{
    internal static readonly ReferenceEqualityComparer<T> Instance = new();

    private ReferenceEqualityComparer()
    {
    }

    public bool Equals(T? x, T? y) => ReferenceEquals(x, y);

    public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
}
