using System.Globalization;

namespace Motiv.Serialization;

/// <summary>
/// Where both stores stand, as one value: a scalar per store, each moving whenever a write lands
/// there. Polled to decide whether a replica needs to rebuild, and stamped on responses so a client
/// can tell it was routed to a replica serving an older world.
/// </summary>
/// <remarks>
/// A pair rather than one number because the two stores are <em>never written in the same
/// transaction</em> — there is no shared sequence to derive. Comparison is therefore component-wise
/// and deliberately not a total order: "am I behind" is answerable, "which of these two is newer"
/// is not, and inventing an answer would be a fiction a caller could act on.
/// </remarks>
/// <param name="Rules">Where the rule store stands.</param>
/// <param name="Propositions">Where the proposition store stands.</param>
public readonly record struct StoreGeneration(long Rules, long Propositions)
{
    /// <summary>Before anything has been read or written.</summary>
    public static StoreGeneration Zero => default;

    /// <summary>Whether either component differs from <paramref name="other"/> — the poll's question.</summary>
    public bool MovedFrom(StoreGeneration other) => this != other;

    /// <summary>
    /// Whether any component is lower than <paramref name="other"/>'s — the client's question, and
    /// the reason this is not an ordering: both directions can be true at once.
    /// </summary>
    public bool IsBehind(StoreGeneration other) =>
        Rules < other.Rules || Propositions < other.Propositions;

    /// <summary>The wire form, as carried by the response header.</summary>
    public string ToToken() =>
        string.Format(CultureInfo.InvariantCulture, "r{0}.p{1}", Rules, Propositions);

    /// <summary>Reads a token written by <see cref="ToToken"/>. Anything else is refused.</summary>
    public static bool TryParseToken(string? token, out StoreGeneration generation)
    {
        generation = Zero;

        if (string.IsNullOrEmpty(token) || token![0] != 'r')
            return false;

        var separator = token.IndexOf(".p", StringComparison.Ordinal);
        if (separator < 1)
            return false;

        var rules = token.Substring(1, separator - 1);
        var propositions = token.Substring(separator + 2);

        if (!long.TryParse(rules, NumberStyles.None, CultureInfo.InvariantCulture, out var ruleValue)
            || !long.TryParse(propositions, NumberStyles.None, CultureInfo.InvariantCulture, out var propositionValue))
        {
            return false;
        }

        generation = new StoreGeneration(ruleValue, propositionValue);
        return true;
    }
}
