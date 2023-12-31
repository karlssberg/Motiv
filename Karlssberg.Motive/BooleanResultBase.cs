using System.Diagnostics;
using Humanizer;
using Karlssberg.Motive.And;
using Karlssberg.Motive.Not;
using Karlssberg.Motive.Or;
using Karlssberg.Motive.XOr;

namespace Karlssberg.Motive;

[DebuggerDisplay("{ToString()}")]
public abstract class BooleanResultBase<TMetadata>
{
    public abstract bool IsSatisfied { get; }

    public abstract string Description { get; }
    
    public abstract IEnumerable<string> Reasons { get; }
    
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
    
    public static explicit operator bool(BooleanResultBase<TMetadata> result) =>
        result.IsSatisfied;
}