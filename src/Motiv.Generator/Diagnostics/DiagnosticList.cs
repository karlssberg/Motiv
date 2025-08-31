using System.Collections;
using Microsoft.CodeAnalysis;

namespace Motiv.Generator.Diagnostics;

public class DiagnosticList : IReadOnlyList<Diagnostic>
{
    private readonly IList<Diagnostic> _readOnlyListImplementation = [];

    public IEnumerator<Diagnostic> GetEnumerator()
    {
        return _readOnlyListImplementation.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable)_readOnlyListImplementation).GetEnumerator();
    }

    public int Count => _readOnlyListImplementation.Count;

    public Diagnostic this[int index] => _readOnlyListImplementation[index];

    public void Add(Diagnostic item)
    {
        _readOnlyListImplementation.Add(item);
    }

    public void AddRange(IEnumerable<Diagnostic> items)
    {
        _readOnlyListImplementation.AddRange(items);
    }

    public void Clear()
    {
        _readOnlyListImplementation.Clear();
    }
}

