using System.Diagnostics;
using Karlssberg.Motiv.And;
using Karlssberg.Motiv.Not;
using Karlssberg.Motiv.Or;
using Karlssberg.Motiv.XOr;

namespace Karlssberg.Motiv;

/// <summary>Represents a base class for boolean results.</summary>
[DebuggerDisplay("{GetSatisfiedText()}: {Explanation.GetDebuggerDisplay()}")]
public abstract class BooleanResultBase
    : IEquatable<BooleanResultBase>,
        IEquatable<bool>
{
    private const string True = "true";
    private const string False = "false";

    /// <summary>Prevent inheritance from outside of this project/assembly.</summary>
    internal BooleanResultBase()
    {
    }

    /// <summary>Gets a value indicating whether the condition is satisfied.</summary>
    public abstract bool Satisfied { get; }

    /// <summary>Gets a set of human readable descriptions of the underlying causes.</summary>
    public abstract ResultDescriptionBase Description { get; }
    
    public string Reason => Description.Compact;
    
    public IEnumerable<string> Assertions => Explanation.Assertions;

    public IEnumerable<string> SubAssertions => Explanation.Underlying.GetAssertions();
    
    /// <summary>
    /// Gets the specific underlying reasons why the condition is satisfied or not. Duplicates are permitted in the
    /// result at this stage to avoid excessive deduplication during intermediate steps.  Deduplication is performed during the
    /// call to <see cref="Explanation" />.
    /// </summary>
    public abstract Explanation Explanation { get; }

    /// <summary>Determines whether the current BooleanResultBase object is equal to another BooleanResultBase object.</summary>
    /// <param name="other">The BooleanResultBase object to compare with the current object.</param>
    /// <returns>true if the current object is equal to the other object; otherwise, false.</returns>
    public bool Equals(BooleanResultBase? other) =>
        other switch
        {
            null => false,
            _ when ReferenceEquals(this, other) => true,
            _ => Satisfied == other.Satisfied
        };

    /// <summary>Determines whether the current BooleanResultBase object is equal to the specified boolean value.</summary>
    /// <param name="other">The boolean value to compare with the current BooleanResultBase object.</param>
    /// <returns>True if the current BooleanResultBase object is equal to the specified boolean value; otherwise, false.</returns>
    public bool Equals(bool other) => other == Satisfied;

    /// <summary>Returns a human readable description of the tree of conditions that make up this result.</summary>
    /// <returns>A string that describes the tree of conditions that make up this result.</returns>
    public override string ToString() => Explanation.ToString();

    /// <summary>Defines the true operator for the <see cref="BooleanResultBase{TMetadata}" /> class.</summary>
    /// <param name="result">The <see cref="BooleanResultBase{TMetadata}" /> instance.</param>
    /// <returns><c>true</c> if the <paramref name="result" /> is satisfied; otherwise, <c>false</c>.</returns>
    public static bool operator true(BooleanResultBase result) =>
        result.Satisfied;

    /// <summary>Defines the false operator for the <see cref="BooleanResultBase{TMetadata}" /> class.</summary>
    /// <param name="result">The <see cref="BooleanResultBase{TMetadata}" /> instance.</param>
    /// <returns><c>true</c> if the <paramref name="result" /> is not satisfied; otherwise, <c>false</c>.</returns>
    public static bool operator false(BooleanResultBase result) =>
        !result.Satisfied;

    /// <summary>Determines whether two <see cref="BooleanResultBase{TMetadata}" /> objects are equal.</summary>
    /// <param name="left">The first <see cref="BooleanResultBase{TMetadata}" /> to compare.</param>
    /// <param name="right">The second <see cref="BooleanResultBase{TMetadata}" /> to compare.</param>
    /// <returns><c>true</c> if the two objects are equal; otherwise, <c>false</c>.</returns>
    public static bool operator ==(BooleanResultBase left, BooleanResultBase right) =>
        left.Equals(right);

    /// <summary>Determines whether two <see cref="BooleanResultBase{TMetadata}" /> objects are equal.</summary>
    /// <param name="left">The first <see cref="BooleanResultBase{TMetadata}" /> to compare.</param>
    /// <param name="right">The second <see cref="bool" /> to compare.</param>
    /// <returns><c>true</c> if the two objects are equal; otherwise, <c>false</c>.</returns>
    public static bool operator ==(bool left, BooleanResultBase right) =>
        left == right.Satisfied;

    public static bool operator !=(bool left, BooleanResultBase right) => !(left == right);

    /// <summary>Determines whether two <see cref="BooleanResultBase{TMetadata}" /> objects are equal.</summary>
    /// <param name="left">The first <see cref="BooleanResultBase{TMetadata}" /> to compare.</param>
    /// <param name="right">The second <see cref="bool" /> to compare.</param>
    /// <returns><c>true</c> if the two objects are equal; otherwise, <c>false</c>.</returns>
    public static bool operator ==(BooleanResultBase left, bool right) =>
        left.Satisfied == right;

    public static bool operator !=(BooleanResultBase left, bool right) => !(left == right);

    /// <summary>Implements the inequality operator for comparing two instances of <see cref="BooleanResultBase{TMetadata}" />.</summary>
    /// <param name="left">The left-hand side <see cref="BooleanResultBase{TMetadata}" /> instance.</param>
    /// <param name="right">The right-hand side <see cref="BooleanResultBase{TMetadata}" /> instance.</param>
    /// <returns><c>true</c> if the two instances are not equal; otherwise, <c>false</c>.</returns>
    public static bool operator !=(BooleanResultBase left, BooleanResultBase right) =>
        !(left == right);

    /// <summary>Defines an explicit conversion from <see cref="BooleanResultBase{TMetadata}" /> to <see cref="bool" />.</summary>
    /// <param name="result">The <see cref="BooleanResultBase{TMetadata}" /> instance to convert.</param>
    /// <returns>The boolean value indicating whether the result is satisfied.</returns>
    public static explicit operator bool(BooleanResultBase result) =>
        result.Satisfied;

    /// <summary>Determines whether the current object is equal to another object.</summary>
    /// <param name="obj">The object to compare with the current object.</param>
    /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
    public override bool Equals(object? obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((BooleanResultBase)obj);
    }

    /// <summary>Computes the hash code for the current BooleanResultBase object.</summary>
    /// <returns>A hash code for the current object.</returns>
    public override int GetHashCode() => Satisfied.GetHashCode();

    /// <summary>Gets the lowercase display text for true or false states.</summary>
    private string GetSatisfiedText() => Satisfied ? True : False;
}

/// <summary>Represents a base class for boolean results with metadata.</summary>
/// <typeparam name="TMetadata">The type of the metadata associated with the boolean result.</typeparam>
public abstract class BooleanResultBase<TMetadata>
    : BooleanResultBase,
        IEquatable<BooleanResultBase<TMetadata>>
{
    /// <summary>Prevent inheritance from outside of this project/assembly.</summary>
    internal BooleanResultBase()
    {
    }

    public abstract MetadataTree<TMetadata> MetadataTree { get; }
    
    public IEnumerable<TMetadata> Metadata => MetadataTree.AsEnumerable();
    
    public abstract IEnumerable<BooleanResultBase> Underlying { get; }
    
    public abstract IEnumerable<BooleanResultBase<TMetadata>> UnderlyingWithMetadata { get; }
    
    public abstract IEnumerable<BooleanResultBase> Causes  { get; }
    
    public abstract IEnumerable<BooleanResultBase<TMetadata>> CausesWithMetadata  { get; }

    /// <summary>Determines whether the current BooleanResultBase object is equal to another BooleanResultBase object.</summary>
    /// <param name="other">The BooleanResultBase object to compare with the current object.</param>
    /// <returns>true if the current object is equal to the other object; otherwise, false.</returns>
    public bool Equals(BooleanResultBase<TMetadata>? other) =>
        other switch
        {
            null => false,
            _ when ReferenceEquals(this, other) => true,
            _ => Satisfied == other.Satisfied
        };

    /// <summary>
    /// Performs a logical AND operation between the current BooleanResultBase instance and another BooleanResultBase
    /// instance.
    /// </summary>
    /// <param name="right">The other BooleanResultBase instance to perform the logical AND operation with.</param>
    /// <returns>A new instance of AndBooleanResult representing the result of the logical AND operation.</returns>
    public BooleanResultBase<TMetadata> And(BooleanResultBase<TMetadata> right) =>
        new AndBooleanResult<TMetadata>(this, right);

    /// <summary>
    /// Performs a logical OR operation between the current BooleanResultBase instance and another BooleanResultBase
    /// instance.
    /// </summary>
    /// <param name="right">The other BooleanResultBase instance to perform the OR operation with.</param>
    /// <returns>A new BooleanResultBase instance representing the result of the OR operation.</returns>
    public BooleanResultBase<TMetadata> Or(BooleanResultBase<TMetadata> right) =>
        new OrBooleanResult<TMetadata>(this, right);

    /// <summary>
    /// Performs a logical exclusive OR (XOR) operation between this BooleanResultBase instance and another
    /// BooleanResultBase instance.
    /// </summary>
    /// <param name="right">The other BooleanResultBase instance to perform the XOR operation with.</param>
    /// <returns>A new XOrBooleanResult instance representing the result of the XOR operation.</returns>
    public BooleanResultBase<TMetadata> XOr(BooleanResultBase<TMetadata> right) =>
        new XOrBooleanResult<TMetadata>(this, right);

    /// <summary>
    /// Returns a new instance of <see cref="NotBooleanResult{TMetadata}" /> that represents the logical negation of
    /// the current instance.
    /// </summary>
    /// <returns>
    /// A new instance of <see cref="NotBooleanResult{TMetadata}" /> that represents the logical negation of the
    /// current instance.
    /// </returns>
    public BooleanResultBase<TMetadata> Not() => new NotBooleanResult<TMetadata>(this);

    /// <summary>Overloads the bitwise AND operator to perform a logical AND operation on two BooleanResultBase instances.</summary>
    /// <param name="left">The left BooleanResultBase instance.</param>
    /// <param name="right">The right BooleanResultBase instance.</param>
    /// <returns>A new BooleanResultBase instance representing the result of the logical AND operation.</returns>
    public static BooleanResultBase<TMetadata> operator &(
        BooleanResultBase<TMetadata> left,
        BooleanResultBase<TMetadata> right) =>
        left.And(right);

    /// <summary>Overloads the logical OR operator (|) to perform a logical OR operation on two BooleanResultBase instances.</summary>
    /// <param name="left">The left BooleanResultBase instance.</param>
    /// <param name="right">The right BooleanResultBase instance.</param>
    /// <returns>A new BooleanResultBase instance representing the result of the logical OR operation.</returns>
    public static BooleanResultBase<TMetadata> operator |(
        BooleanResultBase<TMetadata> left,
        BooleanResultBase<TMetadata> right) =>
        left.Or(right);

    /// <summary>Overloads the ^ operator to perform an exclusive OR (XOR) operation on two BooleanResultBase instances.</summary>
    /// <typeparam name="TMetadata">The type of the metadata associated with the BooleanResultBase.</typeparam>
    /// <param name="left">The left BooleanResultBase operand.</param>
    /// <param name="right">The right BooleanResultBase operand.</param>
    /// <returns>A new BooleanResultBase instance representing the result of the XOR operation.</returns>
    public static BooleanResultBase<TMetadata> operator ^(
        BooleanResultBase<TMetadata> left,
        BooleanResultBase<TMetadata> right) =>
        left.XOr(right);

    /// <summary>Overloads the logical NOT operator for the BooleanResultBase class.</summary>
    /// <param name="result">The BooleanResultBase object to negate.</param>
    /// <returns>A new BooleanResultBase object that represents the negation of the input.</returns>
    public static BooleanResultBase<TMetadata> operator !(
        BooleanResultBase<TMetadata> result) =>
        result.Not();

    /// <summary>Defines the true operator for the <see cref="BooleanResultBase{TMetadata}" /> class.</summary>
    /// <param name="booleanResult">The <see cref="BooleanResultBase{TMetadata}" /> instance.</param>
    /// <returns><c>true</c> if the <paramref name="booleanResult" /> is satisfied; otherwise, <c>false</c>.</returns>
    public static bool operator true(BooleanResultBase<TMetadata> booleanResult) =>
        booleanResult.Satisfied;

    /// <summary>Defines the false operator for the <see cref="BooleanResultBase{TMetadata}" /> class.</summary>
    /// <param name="booleanResult">The <see cref="BooleanResultBase{TMetadata}" /> instance.</param>
    /// <returns><c>true</c> if the <paramref name="booleanResult" /> is not satisfied; otherwise, <c>false</c>.</returns>
    public static bool operator false(BooleanResultBase<TMetadata> booleanResult) =>
        !booleanResult.Satisfied;

    /// <summary>Determines whether two <see cref="BooleanResultBase{TMetadata}" /> objects are equal.</summary>
    /// <param name="left">The first <see cref="BooleanResultBase{TMetadata}" /> to compare.</param>
    /// <param name="right">The second <see cref="BooleanResultBase{TMetadata}" /> to compare.</param>
    /// <returns><c>true</c> if the two objects are equal; otherwise, <c>false</c>.</returns>
    public static bool operator ==(BooleanResultBase<TMetadata> left, BooleanResultBase<TMetadata> right) =>
        left.Equals(right);

    /// <summary>Determines whether two <see cref="BooleanResultBase{TMetadata}" /> objects are equal.</summary>
    /// <param name="left">The first <see cref="BooleanResultBase{TMetadata}" /> to compare.</param>
    /// <param name="right">The second <see cref="bool" /> to compare.</param>
    /// <returns><c>true</c> if the two objects are equal; otherwise, <c>false</c>.</returns>
    public static bool operator ==(bool left, BooleanResultBase<TMetadata> right) =>
        left == right.Satisfied;

    public static bool operator !=(bool left, BooleanResultBase<TMetadata> right) => !(left == right);

    /// <summary>Determines whether two <see cref="BooleanResultBase{TMetadata}" /> objects are equal.</summary>
    /// <param name="left">The first <see cref="BooleanResultBase{TMetadata}" /> to compare.</param>
    /// <param name="right">The second <see cref="bool" /> to compare.</param>
    /// <returns><c>true</c> if the two objects are equal; otherwise, <c>false</c>.</returns>
    public static bool operator ==(BooleanResultBase<TMetadata> left, bool right) =>
        left.Satisfied == right;

    public static bool operator !=(BooleanResultBase<TMetadata> left, bool right) => !(left == right);

    /// <summary>Implements the inequality operator for comparing two instances of <see cref="BooleanResultBase{TMetadata}" />.</summary>
    /// <param name="left">The left-hand side <see cref="BooleanResultBase{TMetadata}" /> instance.</param>
    /// <param name="right">The right-hand side <see cref="BooleanResultBase{TMetadata}" /> instance.</param>
    /// <returns><c>true</c> if the two instances are not equal; otherwise, <c>false</c>.</returns>
    public static bool operator !=(BooleanResultBase<TMetadata> left, BooleanResultBase<TMetadata> right) =>
        !(left == right);

    /// <summary>Defines an explicit conversion from <see cref="BooleanResultBase{TMetadata}" /> to <see cref="bool" />.</summary>
    /// <param name="result">The <see cref="BooleanResultBase{TMetadata}" /> instance to convert.</param>
    /// <returns>The boolean value indicating whether the result is satisfied.</returns>
    public static implicit operator bool(BooleanResultBase<TMetadata> result) =>
        result.Satisfied;

    /// <summary>Determines whether the current object is equal to another object.</summary>
    /// <param name="obj">The object to compare with the current object.</param>
    /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((BooleanResultBase<TMetadata>)obj);
    }

    /// <summary>Computes the hash code for the current BooleanResultBase object.</summary>
    /// <returns>A hash code for the current object.</returns>
    public override int GetHashCode() => Satisfied.GetHashCode();
}