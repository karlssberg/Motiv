// netstandard2.0's BCL predates KeyValuePair<TKey, TValue>.Deconstruct, which arrived with
// netstandard2.1/.NET Core. This extension method restores that call surface for this project only;
// it is not a compiler-emitted marker like the types in NetStandardPolyfills.cs, but an ordinary
// library gap.
//
// It must stay internal so it never leaks as part of this package's public surface, and this file
// compiles only for netstandard2.0 — every other target framework already has the real BCL member
// and must keep using it.
#if NETSTANDARD2_0

namespace System.Collections.Generic
{
    /// <summary>Polyfills for key/value-pair members missing from netstandard2.0.</summary>
    internal static class NetStandardCollectionExtensions
    {
        /// <summary>Deconstructs a <see cref="KeyValuePair{TKey, TValue}"/> for use in a foreach pattern.</summary>
        public static void Deconstruct<TKey, TValue>(
            this KeyValuePair<TKey, TValue> pair, out TKey key, out TValue value)
        {
            key = pair.Key;
            value = pair.Value;
        }
    }
}

#endif
