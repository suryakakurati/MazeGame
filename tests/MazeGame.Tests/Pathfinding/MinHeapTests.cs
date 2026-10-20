using System;
using System.Collections.Generic;
using System.Linq;
using MazeGame.Core.Pathfinding;

namespace MazeGame.Tests.Pathfinding;

/// <summary>
/// Verifies that <see cref="MinHeap{T}"/> always yields items in non-decreasing order, keeps
/// duplicates, handles an empty heap, and honours an injected comparer.
/// </summary>
public sealed class MinHeapTests
{
    [Fact]
    public void NewHeap_IsEmpty()
    {
        var heap = new MinHeap<int>();

        Assert.True(heap.IsEmpty);
        Assert.Equal(0, heap.Count);
    }

    [Fact]
    public void Peek_OnEmptyHeap_Throws()
    {
        var heap = new MinHeap<int>();

        Assert.Throws<InvalidOperationException>(() => heap.Peek());
    }

    [Fact]
    public void ExtractMin_OnEmptyHeap_Throws()
    {
        var heap = new MinHeap<int>();

        Assert.Throws<InvalidOperationException>(() => heap.ExtractMin());
    }

    [Fact]
    public void Insert_ThenPeek_ReturnsSmallestWithoutRemovingIt()
    {
        var heap = new MinHeap<int>();

        heap.Insert(7);
        heap.Insert(3);
        heap.Insert(9);

        Assert.Equal(3, heap.Peek());
        Assert.Equal(3, heap.Count);
    }

    [Fact]
    public void ExtractMin_ReturnsItemsInAscendingOrder()
    {
        var heap = new MinHeap<int>();
        int[] values = [7, 3, 9, 1, 1, 0, 42, -5, 8];

        foreach (var value in values)
        {
            heap.Insert(value);
        }

        var extracted = new List<int>();
        while (!heap.IsEmpty)
        {
            extracted.Add(heap.ExtractMin());
        }

        Assert.Equal(values.OrderBy(value => value), extracted);
        Assert.Equal(values.Length, extracted.Count);
    }

    [Fact]
    public void ExtractMin_OnManyItems_ReturnsEveryItemInAscendingOrder()
    {
        var heap = new MinHeap<int>();
        var values = Enumerable.Range(0, 200).Select(value => (value * 37) % 101).ToArray();

        foreach (var value in values)
        {
            heap.Insert(value);
        }

        var extracted = new List<int>();
        while (!heap.IsEmpty)
        {
            extracted.Add(heap.ExtractMin());
        }

        Assert.Equal(values.OrderBy(value => value), extracted);
    }

    [Fact]
    public void ExtractMin_MixedInsertAndRemove_KeepsOrdering()
    {
        var heap = new MinHeap<int>();

        heap.Insert(5);
        heap.Insert(2);
        Assert.Equal(2, heap.ExtractMin());

        heap.Insert(1);
        heap.Insert(8);
        Assert.Equal(1, heap.ExtractMin());
        Assert.Equal(5, heap.ExtractMin());
        Assert.Equal(8, heap.ExtractMin());
    }

    [Fact]
    public void WithCustomComparer_ExtractsLargestFirst()
    {
        var maxComparer = Comparer<int>.Create((first, second) => second.CompareTo(first));
        var heap = new MinHeap<int>(maxComparer);

        heap.Insert(4);
        heap.Insert(10);
        heap.Insert(1);

        Assert.Equal(10, heap.ExtractMin());
        Assert.Equal(4, heap.ExtractMin());
        Assert.Equal(1, heap.ExtractMin());
    }

    [Fact]
    public void Count_TracksInsertionsAndRemovals()
    {
        var heap = new MinHeap<int>();

        heap.Insert(1);
        heap.Insert(2);
        heap.Insert(3);
        Assert.Equal(3, heap.Count);

        heap.ExtractMin();
        heap.ExtractMin();

        Assert.Equal(1, heap.Count);
        Assert.False(heap.IsEmpty);

        heap.ExtractMin();
        Assert.True(heap.IsEmpty);
    }
}
