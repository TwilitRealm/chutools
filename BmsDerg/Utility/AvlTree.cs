using System.Diagnostics;
using System.Text.Json.Serialization;

namespace BmsDerg.Utility;

public interface INodeInterval<TIndex> where TIndex : IComparable<TIndex>
{
    Interval<TIndex> Interval { get; }
}

public record struct Interval<TIndex>(TIndex StartPos, TIndex EndPos)
    : INodeInterval<TIndex> where TIndex : IComparable<TIndex>
{
    public bool Overlaps(Interval<TIndex> other)
    {
        if (StartPos.CompareTo(other.EndPos) >= 0)
            return false;

        if (EndPos.CompareTo(other.StartPos) <= 0)
            return false;

        return true;
    }

    Interval<TIndex> INodeInterval<TIndex>.Interval => this;
}

public sealed class AvlTree<TIndex, TItem> where TIndex : IComparable<TIndex> where TItem : INodeInterval<TIndex>
{
    [JsonInclude] private Node? _root;

    public void Insert(TItem value)
    {
        _root = AddRecursive(_root, value, out _);
    }

    public bool Remove(TItem value)
    {
        var wasRemoved = false;
        _root = RemoveRecursive(_root, value, out _, ref wasRemoved);
        return wasRemoved;
    }

    public void SearchOverlapping(Interval<TIndex> interval, List<TItem> results)
    {
        SearchOverlappingRecursive(_root, interval, results);
    }

    public List<TItem> SearchOverlapping(Interval<TIndex> interval)
    {
        var list = new List<TItem>();
        SearchOverlapping(interval, list);
        return list;
    }

    private static void SearchOverlappingRecursive(Node? node, Interval<TIndex> interval, List<TItem> results)
    {
        if (node == null)
            return;

        if (interval.StartPos.CompareTo(node.MaxValue) >= 0)
            return;

        if (interval.Overlaps(node.Value.Interval))
            results.Add(node.Value);

        if (node.Left is { } left)
            SearchOverlappingRecursive(left, interval, results);

        if (interval.EndPos.CompareTo(node.Value.Interval.StartPos) > 0 && node.Right is { } right)
            SearchOverlappingRecursive(right, interval, results);
    }

    public List<TItem> GetAll()
    {
        var list = new List<TItem>();
        GetAllRecursive(_root, list);
        return list;
    }

    private static void GetAllRecursive(Node? node, List<TItem> results)
    {
        if (node == null)
            return;

        results.Add(node.Value);

        GetAllRecursive(node.Left, results);
        GetAllRecursive(node.Right, results);
    }

    private static Node AddRecursive(Node? node, TItem value, out bool heightIncreased)
    {
        if (node == null)
        {
            heightIncreased = true;
            return new Node(value);
        }

        var cmp = value.Interval.StartPos.CompareTo(node.Value.Interval.StartPos);
        if (cmp == 0)
            throw new ArgumentException("Duplicate node start position!");

        if (cmp < 0)
        {
            var newNode = AddRecursive(node.Left, value, out var localIncreased);
            node.Left = newNode;
            UpdateMax(node);

            if (localIncreased)
            {
                if (node.Balance > 0)
                {
                    // Inserted on left and was previously right-heavy -> now equal.
                    node.Balance = 0;
                    heightIncreased = false;
                }
                else if (node.Balance == 0)
                {
                    // Inserted on left and previously balanced -> now left-heavy.
                    node.Balance = -1;
                    heightIncreased = true;
                }
                else
                {
                    // Inserted on left and previously left-heavy -> need rebalancing.
                    heightIncreased = false;
                    if (newNode.Balance <= 0)
                    {
                        return RotateRight(node);
                    }
                    else
                    {
                        return RotateLeftRight(node);
                    }
                }
            }
            else
            {
                heightIncreased = false;
            }
        }
        else
        {
            var newNode = AddRecursive(node.Right, value, out var localIncreased);
            node.Right = newNode;
            UpdateMax(node);

            if (localIncreased)
            {
                if (node.Balance < 0)
                {
                    // Inserted on right and was previously left-heavy -> now equal.
                    node.Balance = 0;
                    heightIncreased = false;
                }
                else if (node.Balance == 0)
                {
                    // Inserted on right and previously balanced -> now right-heavy.
                    node.Balance = 1;
                    heightIncreased = true;
                }
                else
                {
                    // Inserted on right and previously right-heavy -> need rebalancing.
                    heightIncreased = false;
                    if (newNode.Balance >= 0)
                    {
                        return RotateLeft(node);
                    }
                    else
                    {
                        return RotateRightLeft(node);
                    }
                }
            }
            else
            {
                heightIncreased = false;
            }
        }

        return node;
    }

    private static Node? RemoveRecursive(Node? node, TItem value, out bool heightDecreased, ref bool wasRemoved)
    {
        if (node == null)
        {
            heightDecreased = false;
            return null;
        }

        var cmp = value.Interval.StartPos.CompareTo(node.Value.Interval.StartPos);
        if (cmp == 0)
        {
            if (node.Left != null && node.Right != null)
            {
                var lowest = FindLowest(node.Right);
                SwapContents(lowest, node);
                cmp = 1;
                goto recurse;
            }

            if (node.Left != null)
            {
                heightDecreased = true;
                wasRemoved = true;
                return node.Left;
            }

            heightDecreased = true;
            wasRemoved = true;
            return node.Right;
        }

        recurse: ;

        if (cmp < 0)
        {
            var newNode = RemoveRecursive(node.Left, value, out var localDecreased, ref wasRemoved);
            node.Left = newNode;
            UpdateMax(node);

            if (localDecreased)
            {
                if (node.Balance > 0)
                {
                    Debug.Assert(node.Right != null);
                    var rightBalance = node.Right.Balance;
                    heightDecreased = rightBalance == 0;

                    if (rightBalance < 0)
                    {
                        return RotateRightLeft(node);
                    }
                    else
                    {
                        return RotateLeft(node);
                    }
                }
                else if (node.Balance == 0)
                {
                    node.Balance = 1;
                    heightDecreased = false;
                    return node;
                }
                else
                {
                    node.Balance = 0;
                    heightDecreased = true;
                    return node;
                }
            }

            heightDecreased = false;
            return node;
        }
        else
        {
            var newNode = RemoveRecursive(node.Right, value, out var localDecreased, ref wasRemoved);
            node.Right = newNode;
            UpdateMax(node);

            if (localDecreased)
            {
                if (node.Balance < 0)
                {
                    Debug.Assert(node.Left != null);
                    var leftBalance = node.Left.Balance;
                    heightDecreased = leftBalance == 0;

                    if (leftBalance > 0)
                    {
                        return RotateLeftRight(node);
                    }
                    else
                    {
                        return RotateRight(node);
                    }
                }
                else if (node.Balance == 0)
                {
                    node.Balance = -1;
                    heightDecreased = false;
                    return node;
                }
                else
                {
                    node.Balance = 0;
                    heightDecreased = true;
                    return node;
                }
            }

            heightDecreased = false;
            return node;
        }

        throw new UnreachableException();
    }

    private static void SwapContents(Node a, Node b)
    {
        (a.Value, b.Value) = (b.Value, a.Value);
    }

    private static Node FindLowest(Node tree)
    {
        while (tree.Left != null)
        {
            tree = tree.Left;
        }

        return tree;
    }

    private static Node RotateLeft(Node parent)
    {
        var newParent = parent.Right;
        Debug.Assert(newParent != null);

        parent.Right = newParent.Left;
        newParent.Left = parent;

        if (newParent.Balance == 0)
        {
            newParent.Balance = -1;
            parent.Balance = 1;
        }
        else
        {
            newParent.Balance = 0;
            parent.Balance = 0;
        }

        UpdateMax(newParent);
        UpdateMax(parent);
        AssertNodeOrderValid(newParent);

        return newParent;
    }

    private static Node RotateRight(Node parent)
    {
        var newParent = parent.Left;
        Debug.Assert(newParent != null);

        parent.Left = newParent.Right;
        newParent.Right = parent;

        if (newParent.Balance == 0)
        {
            newParent.Balance = 1;
            parent.Balance = -1;
        }
        else
        {
            newParent.Balance = 0;
            parent.Balance = 0;
        }

        UpdateMax(newParent);
        UpdateMax(parent);
        AssertNodeOrderValid(newParent);

        return newParent;
    }

    private static Node RotateRightLeft(Node parent)
    {
        var newRight = parent.Right;
        Debug.Assert(newRight != null);

        var newParent = newRight.Left;
        Debug.Assert(newParent != null);

        parent.Right = newParent.Left;
        newRight.Left = newParent.Right;
        newParent.Left = parent;
        newParent.Right = newRight;

        if (newParent.Balance == 0)
        {
            newRight.Balance = 0;
            parent.Balance = 0;
        }
        else if (newParent.Balance > 0)
        {
            parent.Balance = -1;
            newRight.Balance = 0;
        }
        else
        {
            parent.Balance = 0;
            newRight.Balance = 1;
        }

        newParent.Balance = 0;

        UpdateMax(parent);
        UpdateMax(newRight);
        UpdateMax(newParent);
        AssertNodeOrderValid(newParent);

        return newParent;
    }

    private static Node RotateLeftRight(Node parent)
    {
        var newLeft = parent.Left;
        Debug.Assert(newLeft != null);

        var newParent = newLeft.Right;
        Debug.Assert(newParent != null);

        parent.Left = newParent.Right;
        newLeft.Right = newParent.Left;
        newParent.Right = parent;
        newParent.Left = newLeft;

        if (newParent.Balance == 0)
        {
            newLeft.Balance = 0;
            parent.Balance = 0;
        }
        else if (newParent.Balance > 0)
        {
            parent.Balance = 1;
            newLeft.Balance = 0;
        }
        else
        {
            parent.Balance = 0;
            newLeft.Balance = -1;
        }

        newParent.Balance = 0;

        UpdateMax(parent);
        UpdateMax(newLeft);
        UpdateMax(newParent);
        AssertNodeOrderValid(newParent);

        return newParent;
    }

    private static void UpdateMax(Node node)
    {
        node.MaxValue = node.Value.Interval.EndPos;

        if (node.Left is { } left)
            node.MaxValue = Max(node.MaxValue, left.MaxValue);

        if (node.Right is { } right)
            node.MaxValue = Max(node.MaxValue, right.MaxValue);
    }

    [Conditional("DEBUG")]
    private static void AssertNodeOrderValid(Node node)
    {
        if (node.Left is { } left)
        {
            Debug.Assert(left.Value.Interval.StartPos.CompareTo(node.Value.Interval.StartPos) < 0);
        }

        if (node.Right is { } right)
        {
            Debug.Assert(right.Value.Interval.StartPos.CompareTo(node.Value.Interval.StartPos) > 0);
        }
    }

    private static TIndex Max(TIndex a, TIndex b)
    {
        return a.CompareTo(b) > 0 ? a : b;
    }

    private sealed class Node(TItem value)
    {
        public TItem Value = value;
        public sbyte Balance;
        public TIndex MaxValue = value.Interval.StartPos;

        public Node? Left;
        public Node? Right;
    }
}