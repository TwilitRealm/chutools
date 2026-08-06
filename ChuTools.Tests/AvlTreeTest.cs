using System.Text.Json;

using BmsDerg.Utility;

namespace ChuTools.Tests;

public class AvlTreeTest
{
    private static readonly JsonSerializerOptions DumpOptions = new JsonSerializerOptions
    {
        IncludeFields = true, MaxDepth = 128, WriteIndented = true,
    };

    [Test]
    public void Test1()
    {
        var tree = new AvlTree<int, TestEntry>();
        var r = new Random();
        var arr = Enumerable.Range(1, 128).Select(x => new TestEntry(x)).ToArray();
        r.Shuffle(arr);
        foreach (var e in arr)
        {
            tree.Insert(e);
        }

        TestContext.Out.WriteLine(JsonSerializer.Serialize(tree, DumpOptions));
    }

    public record struct TestEntry(int X, int? Y = null) : INodeInterval<int>
    {
        public Interval<int> Interval => new(X, Y ?? X);
    }

    [Test]
    public void TestInsertSearch()
    {
        var tree = new AvlTree<int, TestEntry>();
        tree.Insert(new TestEntry(1, 5));
        tree.Insert(new TestEntry(3, 10));
        tree.Insert(new TestEntry(2, 3));

        Assert.That(tree.SearchOverlapping(new Interval<int>(4, 5)), Is.EquivalentTo([new TestEntry(1, 5), new TestEntry(3, 10)]));
    }

    [Test]
    public void TestInsertRemove()
    {
        var tree = new AvlTree<int, TestEntry>();
        tree.Insert(new TestEntry(1, 5));
        tree.Insert(new TestEntry(3, 10));
        tree.Insert(new TestEntry(2, 3));

        var success = tree.Remove(new TestEntry(3, 10));
        Assert.That(success);

        Assert.That(tree.SearchOverlapping(new Interval<int>(0, 5)), Is.EquivalentTo([new TestEntry(1, 5), new TestEntry(2, 3)]));
    }
}