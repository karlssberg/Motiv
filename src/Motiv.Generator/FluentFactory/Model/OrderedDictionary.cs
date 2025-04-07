using System.Collections;

namespace Motiv.Generator.FluentFactory.Model;

public class OrderedDictionary<TKey, TValue>(IEqualityComparer<TKey>? comparer = null)
    : IDictionary<TKey, TValue>
{
    private readonly Dictionary<TKey, TValue> _dictionary = comparer is not null
                                                            ? new Dictionary<TKey, TValue>(comparer)
                                                            : new Dictionary<TKey, TValue>();
    private readonly List<TKey> _keys = [];

    public ICollection<TValue> Values => GetOrderedItems() .Select(pair => pair.Value).ToList();


    public OrderedDictionary(IEnumerable<KeyValuePair<TKey, TValue>> items, IEqualityComparer<TKey>? comparer = null) : this(comparer)
    {
        foreach (var item in items)
        {
            Add(item);
        }
    }

    public TValue this[TKey key]
    {
        get => _dictionary[key];
        set => _dictionary[key] = value;
    }

    public TValue this[int index] => _dictionary[_keys[index]];

    public bool Remove(KeyValuePair<TKey, TValue> item)
    {
        _keys.Remove(item.Key);
        return _dictionary.Remove(item.Key);
    }

    public int Count => _dictionary.Count;

    public bool IsReadOnly => ((IDictionary<TKey, TValue>)_dictionary).IsReadOnly;

    public ICollection<TKey> Keys => _dictionary.Keys;


    public void Add(TKey key, TValue value)
    {
        _dictionary.Add(key, value);
        _keys.Add(key);
    }

    public void Add(KeyValuePair<TKey, TValue> item)
    {
        Add(item.Key, item.Value);
    }

    public bool TryGetValue(TKey key, out TValue value)
    {
        return _dictionary.TryGetValue(key, out value);
    }

    public bool Remove(TKey key)
    {
        return _dictionary.Remove(key) && _keys.Remove(key);
    }

    public IEnumerable<KeyValuePair<TKey, TValue>> GetOrderedItems()
    {
        return _keys.Select(key => new KeyValuePair<TKey, TValue>(key, _dictionary[key]));
    }

    public bool ContainsKey(TKey key)
    {
        return _dictionary.ContainsKey(key);
    }

    public void Clear()
    {
        _dictionary.Clear();
        _keys.Clear();
    }

    public bool Contains(KeyValuePair<TKey, TValue> item)
    {
        return _dictionary.Contains(item);
    }

    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
    {
        ((IDictionary<TKey, TValue>)_dictionary).CopyTo(array, arrayIndex);
        _keys.CopyTo(array.Select(pair => pair.Key).ToArray(), arrayIndex);
    }

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        return GetOrderedItems().GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable)GetOrderedItems()).GetEnumerator();
    }

    public TValue GetOrAdd(TKey key, Func<TValue> getValue)
    {
        if (TryGetValue(key, out var existingValue)) return existingValue;

        var value = getValue();
        Add(key, value);
        return value;
    }
}
