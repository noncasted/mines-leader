namespace Console;

// The rows of a window of a trace: children right under their parent, by their start, only under the expanded spans. Both the
// startup trace of a client and a backend trace of the dashboard draw their spans through it.
public sealed class SpanTree<T>
{
    public SpanTree(IReadOnlyList<T> spans, Func<T, int> index, Func<T, int> parent, Func<T, double> start)
    {
        Spans = spans;
        _index = index;
        _start = start;
        _children = spans.ToLookup(parent);
    }

    public const int NoParent = -1;

    private readonly Func<T, int> _index;
    private readonly Func<T, double> _start;
    private readonly ILookup<int, T> _children;

    public IReadOnlyList<T> Spans { get; }

    public IEnumerable<int> Roots => _children[NoParent].Select(_index);
    public IEnumerable<int> All => Spans.Select(_index);

    public bool HasChildren(int index)
    {
        return _children.Contains(index);
    }

    // A stack: a depth is limited only by the number of spans.
    public IReadOnlyList<T> Visible(IReadOnlySet<int> expanded)
    {
        var rows = new List<T>();
        var stack = new Stack<T>(Ordered(NoParent));

        while (stack.TryPop(out var span))
        {
            rows.Add(span);

            if (!expanded.Contains(_index(span)))
                continue;

            foreach (var child in Ordered(_index(span)))
                stack.Push(child);
        }

        return rows;
    }

    // Reversed for the stack.
    private IEnumerable<T> Ordered(int parent)
    {
        return _children[parent].OrderByDescending(_start).ThenByDescending(_index);
    }
}
