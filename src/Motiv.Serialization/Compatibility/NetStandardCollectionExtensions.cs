// netstandard2.0's BCL predates both System.Collections.Generic.CollectionExtensions.GetValueOrDefault
// and KeyValuePair<TKey, TValue>.Deconstruct — both were added with netstandard2.1/.NET Core. These
// extension methods restore that call surface for this project only; they are not compiler-emitted
// markers like the types in NetStandardPolyfills.cs, but ordinary library gaps.
//
// These must stay internal so they never leak as part of this package's public surface, and this
// file compiles only for netstandard2.0 — every other target framework already has the real BCL
// members and must keep using them.
#if NETSTANDARD2_0

namespace System.Collections.Generic
{
    /// <summary>Polyfills for dictionary and key/value-pair members missing from netstandard2.0.</summary>
    internal static class NetStandardCollectionExtensions
    {
        /// <summary>Deconstructs a <see cref="KeyValuePair{TKey, TValue}"/> for use in a foreach pattern.</summary>
        public static void Deconstruct<TKey, TValue>(
            this KeyValuePair<TKey, TValue> pair, out TKey key, out TValue value)
        {
            key = pair.Key;
            value = pair.Value;
        }

        /// <summary>Looks up a key, returning <c>default</c> when it is absent.</summary>
        public static TValue? GetValueOrDefault<TKey, TValue>(
            this IReadOnlyDictionary<TKey, TValue> dictionary, TKey key) =>
            dictionary.TryGetValue(key, out var value) ? value : default;

        /// <summary>Looks up a key, returning <paramref name="defaultValue"/> when it is absent.</summary>
        public static TValue GetValueOrDefault<TKey, TValue>(
            this IReadOnlyDictionary<TKey, TValue> dictionary, TKey key, TValue defaultValue) =>
            dictionary.TryGetValue(key, out var value) ? value : defaultValue;
    }
}

#endif
