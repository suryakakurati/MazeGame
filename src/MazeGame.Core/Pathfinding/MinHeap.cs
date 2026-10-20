using System;
using System.Collections.Generic;

namespace MazeGame.Core.Pathfinding;

/// <summary>
/// A hand-written binary min-heap: a priority queue that always hands back the smallest
/// item first, in <c>O(log n)</c> time per insertion or removal.
/// </summary>
/// <typeparam name="T">The type of item stored. Ordering is decided by a comparer.</typeparam>
/// <remarks>
/// <para>
/// The heap is a <b>complete binary tree stored in a flat list</b>. A node at index
/// <c>i</c> has its children at <c>2i + 1</c> and <c>2i + 2</c> and its parent at
/// <c>(i - 1) / 2</c>, so no pointers or node objects are needed and the array stays
/// packed (no wasted slots).
/// </para>
/// <para>
/// The heap property is: every parent compares no greater than its children. That alone
/// guarantees the smallest item sits at index 0, which is why <see cref="Peek"/> is
/// <c>O(1)</c>. Inserting and removing both repair the property by swapping items along a
/// single root-to-leaf path:
/// </para>
/// <list type="bullet">
///   <item><description><b>Sift up</b> after an insert: the new item bubbles up while it is
///   smaller than its parent.</description></item>
///   <item><description><b>Sift down</b> after a removal: the item moved into the root
///   sinks while a child is smaller than it.</description></item>
/// </list>
/// <para>
/// The comparer is injected, so the same class is a min-heap by default and a max-heap
/// (or any other order) when given a different comparer. Dijkstra supplies entries that
/// compare by distance.
/// </para>
/// <para>
/// This class is deliberately not thread-safe: it is used from one game loop.
/// </para>
/// </remarks>
public sealed class MinHeap<T>
{
    private readonly List<T> _items = [];
    private readonly IComparer<T> _comparer;

    /// <summary>
    /// Creates an empty heap.
    /// </summary>
    /// <param name="comparer">
    /// Decides which item is smaller. When <see langword="null"/>, the default comparer for
    /// <typeparamref name="T"/> is used, which requires <typeparamref name="T"/> to be
    /// comparable with itself.
    /// </param>
    public MinHeap(IComparer<T>? comparer = null)
    {
        _comparer = comparer ?? Comparer<T>.Default;
    }

    /// <summary>
    /// Gets the number of items currently in the heap.
    /// </summary>
    public int Count => _items.Count;

    /// <summary>
    /// Gets a value indicating whether the heap holds no items.
    /// </summary>
    public bool IsEmpty => _items.Count == 0;

    /// <summary>
    /// Returns the smallest item without removing it.
    /// </summary>
    /// <returns>The item at the root of the heap.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the heap is empty.</exception>
    public T Peek()
    {
        if (_items.Count == 0)
        {
            throw new InvalidOperationException("Cannot peek at an empty heap.");
        }

        return _items[0];
    }

    /// <summary>
    /// Adds <paramref name="item"/> to the heap.
    /// </summary>
    /// <param name="item">The item to add. Duplicates are allowed.</param>
    public void Insert(T item)
    {
        // Append at the end (keeping the tree complete), then restore the heap property by
        // bubbling the new item up until its parent is no larger.
        _items.Add(item);
        SiftUp(_items.Count - 1);
    }

    /// <summary>
    /// Removes and returns the smallest item.
    /// </summary>
    /// <returns>The item that was at the root of the heap.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the heap is empty.</exception>
    public T ExtractMin()
    {
        if (_items.Count == 0)
        {
            throw new InvalidOperationException("Cannot extract from an empty heap.");
        }

        var smallest = _items[0];

        // Move the last leaf into the root so the tree stays complete, then let it sink to
        // wherever it belongs. Removing the last element of a list is O(1), so the whole
        // operation costs one sift-down: O(log n).
        var lastIndex = _items.Count - 1;
        _items[0] = _items[lastIndex];
        _items.RemoveAt(lastIndex);

        if (_items.Count > 0)
        {
            SiftDown(0);
        }

        return smallest;
    }

    /// <summary>
    /// Moves the item at <paramref name="index"/> towards the root while it is smaller than
    /// its parent.
    /// </summary>
    private void SiftUp(int index)
    {
        while (index > 0)
        {
            var parentIndex = (index - 1) / 2;

            // Stop as soon as the parent is already no larger: everything above is then
            // correctly ordered too, because the heap was valid before the insert.
            if (_comparer.Compare(_items[index], _items[parentIndex]) >= 0)
            {
                return;
            }

            Swap(index, parentIndex);
            index = parentIndex;
        }
    }

    /// <summary>
    /// Moves the item at <paramref name="index"/> towards the leaves while one of its
    /// children is smaller.
    /// </summary>
    private void SiftDown(int index)
    {
        while (true)
        {
            var leftChild = (2 * index) + 1;
            var rightChild = leftChild + 1;
            var smallest = index;

            // Only compare children that actually exist: a complete tree's last level may
            // stop after a left child.
            if (leftChild < _items.Count && _comparer.Compare(_items[leftChild], _items[smallest]) < 0)
            {
                smallest = leftChild;
            }

            if (rightChild < _items.Count && _comparer.Compare(_items[rightChild], _items[smallest]) < 0)
            {
                smallest = rightChild;
            }

            if (smallest == index)
            {
                return;
            }

            Swap(index, smallest);
            index = smallest;
        }
    }

    /// <summary>
    /// Exchanges two items in the backing list.
    /// </summary>
    private void Swap(int first, int second) =>
        (_items[first], _items[second]) = (_items[second], _items[first]);
}
