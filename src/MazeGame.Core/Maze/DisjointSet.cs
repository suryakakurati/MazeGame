namespace MazeGame.Core.Maze;

/// <summary>
/// A disjoint-set (union-find) forest over the integer range
/// <c>0 .. elementCount - 1</c>, used by the maze generator to decide whether two
/// cells are already connected before a wall between them is removed.
/// </summary>
/// <remarks>
/// <para>
/// Two classic optimisations are implemented by hand here because they are part of
/// the graded material and are presented in class:
/// </para>
/// <list type="bullet">
///   <item>
///     <description><b>Path compression</b> — <see cref="Find"/> re-points every node on
///     the search path directly at the root, so later lookups are almost free.</description>
///   </item>
///   <item>
///     <description><b>Union by size</b> — <see cref="Union"/> always hangs the smaller
///     tree under the root of the larger one, which keeps trees shallow.</description>
///   </item>
/// </list>
/// <para>
/// Together these give an amortised cost that is effectively constant — formally
/// <c>O(α(n))</c> per operation, where α is the inverse Ackermann function.
/// The implementation stores the forest in two flat arrays instead of objects:
/// element indices are contiguous, so arrays are faster and simpler than a node graph.
/// </para>
/// </remarks>
public sealed class DisjointSet
{
    // _parent[i] is the parent of element i. A root is its own parent (_parent[i] == i),
    // which is how we recognise a set's representative without a separate flag.
    private readonly int[] _parent;

    // _size[root] is the number of elements in that root's tree. Only entries at roots
    // are meaningful; union by size reads these to decide which root survives.
    private readonly int[] _size;

    // Number of separate sets that currently exist. Starts at elementCount and drops by
    // one on every successful union, so it reaches 1 for a fully connected maze.
    private int _setCount;

    /// <summary>
    /// Creates <paramref name="elementCount"/> singleton sets, one per element.
    /// </summary>
    /// <param name="elementCount">
    /// Number of elements. Zero is allowed and produces an empty forest; negatives are
    /// rejected because they cannot describe a set of cells.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="elementCount"/> is negative.
    /// </exception>
    public DisjointSet(int elementCount)
    {
        if (elementCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(elementCount),
                elementCount,
                "The number of elements cannot be negative.");
        }

        _parent = new int[elementCount];
        _size = new int[elementCount];

        // Every element starts as the root of its own single-element set.
        for (var element = 0; element < elementCount; element++)
        {
            _parent[element] = element;
            _size[element] = 1;
        }

        _setCount = elementCount;
    }

    /// <summary>
    /// Gets the number of disjoint sets that currently exist (initially the element
    /// count, and 1 once everything is connected).
    /// </summary>
    public int Count => _setCount;

    /// <summary>
    /// Returns the representative (root) of the set containing
    /// <paramref name="element"/>, compressing the path so future lookups are cheaper.
    /// </summary>
    /// <param name="element">Element index in the range <c>0 .. elementCount - 1</c>.</param>
    /// <returns>The root index shared by every element of the same set.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="element"/> is outside the valid range.
    /// </exception>
    public int Find(int element)
    {
        ValidateElement(element);

        // Phase 1: walk up to the root. The root is the only node that is its own parent.
        var root = element;
        while (_parent[root] != root)
        {
            root = _parent[root];
        }

        // Phase 2: path compression. Walk the same path again and hang every node we
        // passed directly off the root, flattening the tree. This is what keeps the
        // cost near constant on repeated lookups.
        var current = element;
        while (_parent[current] != root)
        {
            var next = _parent[current];
            _parent[current] = root;
            current = next;
        }

        return root;
    }

    /// <summary>
    /// Merges the sets containing <paramref name="first"/> and
    /// <paramref name="second"/>, using union by size to keep the trees shallow.
    /// </summary>
    /// <param name="first">Element index in the range <c>0 .. elementCount - 1</c>.</param>
    /// <param name="second">Element index in the range <c>0 .. elementCount - 1</c>.</param>
    /// <returns>
    /// <see langword="true"/> when the two sets were different and have been merged;
    /// <see langword="false"/> when they were already connected (which is exactly the
    /// condition the maze generator uses to reject a wall that would create a cycle).
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when either element is outside the valid range.
    /// </exception>
    public bool Union(int first, int second)
    {
        var firstRoot = Find(first);
        var secondRoot = Find(second);

        // Same root means same set: merging would create a cycle, so report "no change".
        if (firstRoot == secondRoot)
        {
            return false;
        }

        // Union by size: make firstRoot the larger tree so we attach the smaller one
        // underneath. This bounds tree height, which bounds Find's walk length.
        if (_size[firstRoot] < _size[secondRoot])
        {
            (firstRoot, secondRoot) = (secondRoot, firstRoot);
        }

        _parent[secondRoot] = firstRoot;
        _size[firstRoot] += _size[secondRoot];

        // Two sets became one; the absorbed root's size entry is now stale but unused,
        // because only roots are ever read.
        _setCount--;
        return true;
    }

    /// <summary>
    /// Determines whether two elements currently belong to the same set.
    /// </summary>
    /// <param name="first">Element index in the range <c>0 .. elementCount - 1</c>.</param>
    /// <param name="second">Element index in the range <c>0 .. elementCount - 1</c>.</param>
    /// <returns><see langword="true"/> when both elements share a root.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when either element is outside the valid range.
    /// </exception>
    public bool IsConnected(int first, int second) => Find(first) == Find(second);

    /// <summary>
    /// Guards every public operation against indices the flat arrays cannot represent.
    /// </summary>
    /// <param name="element">Element index supplied by the caller.</param>
    private void ValidateElement(int element)
    {
        if (element < 0 || element >= _parent.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(element),
                element,
                $"Element index must be between 0 and {_parent.Length - 1}.");
        }
    }
}
