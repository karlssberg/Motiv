using System.Collections;
using System.Diagnostics;
using Humanizer;
using Karlssberg.Motiv.And;
using Karlssberg.Motiv.Not;
using Karlssberg.Motiv.Or;
using Karlssberg.Motiv.XOr;

namespace Karlssberg.Motiv;

[DebuggerDisplay("{ToString()}")]
public abstract class BooleanResultBase<TMetadata>
    : IEquatable<BooleanResultBase<TMetadata>>,
        IEquatable<bool>,
        IEnumerable<TMetadata>
{
    protected const string True = "true";
    protected const string False = "false";
    public abstract bool IsSatisfied { get; }

    public abstract string Description { get; }

    public abstract IEnumerable<string> Reasons { get; }
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public IEnumerator<TMetadata> GetEnumerator() => this.GetInsights().GetEnumerator();

    public bool Equals(bool other) => IsSatisfied == other;

    public bool Equals(BooleanResultBase<TMetadata>? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return IsSatisfied == other.IsSatisfied;
    }

    public BooleanResultBase<TMetadata> And(BooleanResultBase<TMetadata> otherResult) =>
        new AndBooleanResult<TMetadata>(this, otherResult);

    public BooleanResultBase<TMetadata> Or(BooleanResultBase<TMetadata> otherResult) =>
        new OrBooleanResult<TMetadata>(this, otherResult);

    public BooleanResultBase<TMetadata> XOr(BooleanResultBase<TMetadata> otherResult) =>
        new XOrBooleanResult<TMetadata>(this, otherResult);

    public BooleanResultBase<TMetadata> Not() =>
        new NotBooleanResult<TMetadata>(this);

    public override string ToString() => Reasons.Humanize();

    public static BooleanResultBase<TMetadata> operator &(
        BooleanResultBase<TMetadata> leftResult,
        BooleanResultBase<TMetadata> rightResult) =>
        leftResult.And(rightResult);

    public static BooleanResultBase<TMetadata> operator |(
        BooleanResultBase<TMetadata> leftResult,
        BooleanResultBase<TMetadata> rightResult) =>
        leftResult.Or(rightResult);

    public static BooleanResultBase<TMetadata> operator ^(
        BooleanResultBase<TMetadata> leftResult,
        BooleanResultBase<TMetadata> rightResult) =>
        leftResult.XOr(rightResult);

    public static BooleanResultBase<TMetadata> operator !(
        BooleanResultBase<TMetadata> result) =>
        result.Not();

    public static bool operator true(BooleanResultBase<TMetadata> result) =>
        result.IsSatisfied;

    public static bool operator false(BooleanResultBase<TMetadata> result) =>
        !result.IsSatisfied;

    public static bool operator ==(BooleanResultBase<TMetadata> leftResult, BooleanResultBase<TMetadata> rightResult) =>
        leftResult.Equals(rightResult);

    public static bool operator !=(BooleanResultBase<TMetadata> leftResult, BooleanResultBase<TMetadata> rightResult) =>
        !(leftResult == rightResult);

    public static explicit operator bool(BooleanResultBase<TMetadata> result) =>
        result.IsSatisfied;

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((BooleanResultBase<TMetadata>)obj);
    }

    public override int GetHashCode() => IsSatisfied.GetHashCode();

    protected string IsSatisfiedDisplayText => IsSatisfied ? True : False;
}