using System.Collections.Immutable;

namespace Motiv.Generator.FluentBuilder.FluentModel;

public class Trie<TKey, TValue>
{
    // Root node of the trie
    public Node Root { get; } = new([]);

    // Function to insert a new sequence in the trie
    public void Insert(IEnumerable<TKey> key, TValue value)
    {
        // Start from the root node
        var curr = Root;
        ImmutableArray<TKey> currKey = [];
        // Iterate over the key
        foreach (var keyPart in key)
        {
            currKey = currKey.Add(keyPart);
            // Create a new node if the path doesn't exist
            if (!curr.Children.ContainsKey(keyPart))
            {
                curr.Children.Add(keyPart, new Node(currKey));
            }

            // Move to the next node
            curr = curr.Children[keyPart];
            curr.Values.Add(value);
            curr.EncounteredKeyParts.Add(keyPart);
        }

        // Mark the end of the sequence
        curr.IsEnd = true;
        curr.EndValues.Add(value);
    }

    // Function to search for a sequence in the trie
    public bool Contains(IEnumerable<TKey> key)
    {
        // Start from the root node
        var curr = Root;

        // Iterate over the key
        foreach (var k in key)
        {
            // Move to the next node
            if (!curr.Children.TryGetValue(k, out var child))
            {
                return false;
            }

            curr = child;
        }

        // Return true if the end of the sequence is reached
        return curr.IsEnd;
    }

    // Function to delete a sequence from the trie
    public void Delete(IEnumerable<TKey> key)
    {
        Delete(Root, key as IList<TKey> ?? key.ToArray(), 0);
    }

    // Function to delete a sequence from the trie
    private static bool Delete(Node curr, IList<TKey> key, int index)
    {
        // Return false if the key is not found
        if (index == key.Count)
        {
            return false;
        }

        // Recur for the next node
        if (!curr.Children.ContainsKey(key[index]))
        {
            return false;
        }

        var shouldDeleteCurrentNode = Delete(curr.Children[key[index]], key, index + 1);

        // If true is returned, delete the current node
        if (shouldDeleteCurrentNode)
        {
            curr.Children.Remove(key[index]);

            // Return true if no child nodes are present
            return curr.Children.Count == 0;
        }

        // Return false
        return false;
    }

    public class Node(ImmutableArray<TKey> key)
    {
        public ImmutableArray<TKey> Key { get; } = key;
        public OrderedDictionary<TKey, Node> Children { get; } = new();
        public bool IsEnd { get; set; } = false;
        public IList<TValue> Values { get; } = new List<TValue>();

        public IList<TValue> EndValues { get; } = new List<TValue>();

        public ICollection<TKey> EncounteredKeyParts { get; } = [];
    }
}
