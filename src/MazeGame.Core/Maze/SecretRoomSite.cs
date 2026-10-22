namespace MazeGame.Core.Maze;

/// <summary>
/// Where a floor's secret room will be carved, decided before the maze is generated.
/// </summary>
/// <remarks>
/// <para>
/// The site is a 3x3 block one cell in from the grid's border. It comes in two flavours, and the
/// difference is the whole reason this type exists:
/// </para>
/// <list type="bullet">
///   <item>
///     <description>
///       <b>Rock</b> (<see cref="Reserved"/> false): the shape left these nine cells outside the
///       cave, so the maze was never carved there and the room is hidden in the rock at the edge of
///       the floor.
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Reserved</b> (<see cref="Reserved"/> true): the floor had no rock big enough, so the
///       nine cells are closed <i>before</i> Kruskal runs. The maze is generated around the pocket
///       and the room is opened afterwards, which is how a rectangular floor gets the same chance
///       as a cave — without ever rerouting a corridor, because no corridor was ever carved there.
///     </description>
///   </item>
/// </list>
/// <para>
/// Either way the guarantee the rest of the game relies on is identical: no original passage is
/// removed, the remaining cells stay connected, and the room joins the maze through exactly one
/// door.
/// </para>
/// </remarks>
/// <param name="Row">Row of the block's top-left cell.</param>
/// <param name="Column">Column of the block's top-left cell.</param>
/// <param name="Reserved">Whether the pocket must be closed before the maze is generated.</param>
public readonly record struct SecretRoomSite(int Row, int Column, bool Reserved);
