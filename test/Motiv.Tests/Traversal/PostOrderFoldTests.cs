using Motiv.Traversal;

namespace Motiv.Tests.Traversal;

public class PostOrderFoldTests
{
    private sealed class Node(string name, params Node[] children)
    {
        public string Name { get; } = name;

        public IReadOnlyList<Node> Children { get; } = children;
    }

    private sealed class Recorder
    {
        private readonly Dictionary<Node, string> _memo = new();

        public List<string> Combined { get; } = [];

        public string Fold(Node root) =>
            PostOrderFold.Fold<Node, string>(
                root,
                node => node.Children,
                (node, values) =>
                {
                    Combined.Add(node.Name);
                    return values.Count == 0
                        ? node.Name
                        : $"{node.Name}({string.Join(",", values)})";
                },
                node => _memo.TryGetValue(node, out var value) ? value : null,
                (node, value) => _memo[node] = value);
    }

    [Fact]
    public void Should_combine_children_in_post_order()
    {
        var tree = new Node("root", new Node("a", new Node("a1"), new Node("a2")), new Node("b"));
        var recorder = new Recorder();

        recorder.Fold(tree);

        recorder.Combined.ShouldBe(["a1", "a2", "a", "b", "root"]);
    }

    [Fact]
    public void Should_supply_child_values_in_descend_order()
    {
        var tree = new Node("root", new Node("a", new Node("a1"), new Node("a2")), new Node("b"));

        new Recorder().Fold(tree).ShouldBe("root(a(a1,a2),b)");
    }

    [Fact]
    public void Should_return_the_cached_value_of_an_already_folded_root_without_combining()
    {
        var leaf = new Node("leaf");
        var recorder = new Recorder();

        recorder.Fold(leaf);
        recorder.Fold(leaf);

        recorder.Combined.ShouldBe(["leaf"]);
    }

    [Fact]
    public void Should_combine_a_shared_node_exactly_once()
    {
        var shared = new Node("shared", new Node("s1"));
        var tree = new Node("root", new Node("a", shared), new Node("b", shared));
        var recorder = new Recorder();

        var result = recorder.Fold(tree);

        recorder.Combined.Count(name => name == "shared").ShouldBe(1);
        result.ShouldBe("root(a(shared(s1)),b(shared(s1)))");
    }

    [Fact]
    public void Should_only_descend_into_the_nodes_the_descend_function_selects()
    {
        var skipped = new Node("skipped", new Node("never"));
        var tree = new Node("root", skipped);
        var combined = new List<string>();

        PostOrderFold.Fold<Node, string>(
            tree,
            node => node.Name == "root" ? node.Children : [],
            (node, values) =>
            {
                combined.Add(node.Name);
                return node.Name;
            },
            _ => null,
            (_, _) => { });

        combined.ShouldBe(["skipped", "root"]);
    }

    [Fact]
    public void Should_fold_a_spine_far_deeper_than_the_stack_would_allow()
    {
        var node = new Node("leaf");
        for (var i = 0; i < 100_000; i++)
            node = new Node($"n{i}", node);

        var depth = PostOrderFold.Fold<Node, StrongBox<int>>(
            node,
            n => n.Children,
            (_, values) => new StrongBox<int>(values.Count == 0 ? 1 : values[0].Value + 1),
            _ => null,
            (_, _) => { });

        depth.Value.ShouldBe(100_001);
    }

    private sealed class StrongBox<T>(T value)
    {
        public T Value { get; } = value;
    }
}
