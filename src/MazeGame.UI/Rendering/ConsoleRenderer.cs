using System;
using System.Collections.Generic;
using System.Text;
using MazeGame.Core.Game;
using MazeGame.Core.Maze;

namespace MazeGame.UI.Rendering;

/// <summary>
/// Draws the maze and its contents as ASCII art in the terminal.
/// </summary>
/// <remarks>
/// <para>
/// This is the fallback view for machines without a window, and it plays by the same rules as
/// the graphical one — including fog of war. It therefore hides what
/// <see cref="GameState.IsVisible"/> says the player cannot see; hiding is a presentation
/// choice, the rule itself lives in Core.
/// </para>
/// <para>
/// The one thing the renderer remembers is which cells the player has already seen, so explored
/// walls and floors stay drawn faintly ('.') instead of vanishing when the torch moves on.
/// Entities are only drawn where the player can see them right now.
/// </para>
/// </remarks>
public sealed class ConsoleRenderer : IRenderer
{
    private const char PlayerSymbol = '@';
    private const char EnemySymbol = 'K';
    private const char BarrierSymbol = '#';
    private const char SpentBarrierSymbol = '=';
    private const char LureSymbol = '*';
    private const char KeySymbol = 'k';
    private const char ExitSymbol = 'E';
    private const char CursorSymbol = 'X';
    private const char EmptySymbol = ' ';
    private const char RememberedSymbol = '.';

    private readonly HashSet<Cell> _explored = [];

    /// <inheritdoc />
    public void Draw(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var frame = BuildFrame(state);

        // Console.Clear throws when output is redirected (a log file, a pipe). Drawing the
        // frames one after another still works there, which keeps smoke tests possible.
        if (!Console.IsOutputRedirected)
        {
            Console.Clear();
        }

        Console.Write(frame);
    }

    /// <summary>
    /// Builds the whole frame as a string so it can be written in one go, noting newly seen
    /// cells on the way.
    /// </summary>
    private string BuildFrame(GameState state)
    {
        var frame = new StringBuilder();

        frame.Append("keys ")
            .Append(state.KeysCollected)
            .Append('/')
            .Append(state.Config.KeyCount)
            .Append("   barriers ")
            .Append(state.BarriersRemaining)
            .Append("   wisps ")
            .Append(state.LuresRemaining)
            .Append("   exit ")
            .Append(state.IsExitUnlocked ? "OPEN" : "LOCKED")
            .Append("   ")
            .Append(state.Status)
            .Append(state.AimMode switch
            {
                PlacementMode.Barrier => "   AIMING A BARRIER",
                PlacementMode.Lure => "   AIMING A WISP",
                _ => string.Empty,
            })
            .AppendLine();

        AppendMaze(frame, state);

        frame.AppendLine(state.IsAiming
            ? "AIMING: WASD / arrows move the reticle    Space or Enter: place    Esc: cancel"
            : "WASD / arrows: move    B: aim a barrier    R: aim a wisp    Q: quit");

        return frame.ToString();
    }

    /// <summary>
    /// Appends the maze, one screen row of cell rows at a time (a wall row, then a cell row).
    /// Cell groups the player has never seen are left blank, including their outer edge, so the
    /// fog does not reveal the size or shape of the unexplored part of the maze.
    /// </summary>
    private void AppendMaze(StringBuilder frame, GameState state)
    {
        var maze = state.Maze;

        for (var row = 0; row < maze.Rows; row++)
        {
            AppendWallRow(frame, state, row);
            AppendCellRow(frame, state, row);
        }

        AppendWallRow(frame, state, row: null);
    }

    /// <summary>
    /// Appends the top edge of a row of cells, or the bottom edge of the maze when
    /// <paramref name="row"/> is <see langword="null"/>.
    /// </summary>
    private void AppendWallRow(StringBuilder frame, GameState state, int? row)
    {
        var maze = state.Maze;
        var last = maze.Columns - 1;
        var knownLast = false;

        for (var column = 0; column < maze.Columns; column++)
        {
            var known = row is { } value
                ? IsKnown(state, maze.GetCell(value, column))
                : IsKnown(state, maze.GetCell(maze.Rows - 1, column));

            if (column == last)
            {
                knownLast = known;
            }

            // A wall row is the top edge of its cells; the bottom edge of the maze is a wall
            // row for the last row of cells, so it is always solid.
            var open = row is { } current && known && !maze.HasWall(current, column, Direction.North);
            frame.Append(!known ? "    " : open ? "+   " : "+---");
        }

        frame.AppendLine(knownLast ? "+" : " ");
    }

    /// <summary>Appends the cell symbols of one row, leaving unknown cells blank.</summary>
    private void AppendCellRow(StringBuilder frame, GameState state, int row)
    {
        var maze = state.Maze;
        var last = maze.Columns - 1;
        var knownLast = false;

        for (var column = 0; column < maze.Columns; column++)
        {
            var cell = maze.GetCell(row, column);
            var known = IsKnown(state, cell);

            if (column == last)
            {
                knownLast = known;
            }

            if (!known)
            {
                frame.Append("    ");
                continue;
            }

            frame.Append(maze.HasWall(row, column, Direction.West) ? '|' : ' ');
            frame.Append(' ')
                .Append(SymbolAt(state, cell))
                .Append(' ');
        }

        frame.AppendLine(knownLast ? "|" : " ");
    }

    /// <summary>
    /// Reports whether the player has ever seen <paramref name="cell"/>, which is what decides
    /// whether its walls and floor are drawn at all. A visible cell is recorded on the way, so
    /// it stays known after the torch moves on.
    /// </summary>
    private bool IsKnown(GameState state, Cell cell)
    {
        if (!state.IsVisible(cell))
        {
            return _explored.Contains(cell);
        }

        _explored.Add(cell);
        return true;
    }

    /// <summary>
    /// Reports whether <paramref name="cell"/> holds a barrier, and which of the two symbols it
    /// gets: a sealed barrier still reads as a solid wall, a spent one as a broken husk.
    /// </summary>
    /// <param name="state">The game to read the barriers from.</param>
    /// <param name="cell">The cell to test.</param>
    /// <param name="symbol">The character to draw, or <c>'\0'</c> when there is no barrier.</param>
    /// <returns><see langword="true"/> when the cell holds a barrier.</returns>
    private static bool SymbolForBarrier(GameState state, Cell cell, out char symbol)
    {
        if (!state.Barriers.Contains(cell))
        {
            symbol = default;
            return false;
        }

        symbol = state.BarrierSeals.ContainsKey(cell) ? BarrierSymbol : SpentBarrierSymbol;
        return true;
    }

    /// <summary>
    /// Chooses the character for a cell the player has seen. The player is drawn on top of
    /// everything, because that is the one thing the player must always be able to find.
    /// </summary>
    private static char SymbolAt(GameState state, Cell cell)
    {
        // The aiming cursor outranks everything: while aiming, the player needs to see exactly
        // which cell the barrier will land on, even if that cell is still in the dark.
        if (state.IsAiming && cell == state.AimCursor)
        {
            return CursorSymbol;
        }

        if (cell == state.Player)
        {
            return PlayerSymbol;
        }

        // Remembered cells keep their own barriers (the player put them there) and their floor,
        // but not the killer, the keys or the exit: those must be seen to be known.
        if (!state.IsVisible(cell))
        {
            return SymbolForBarrier(state, cell, out var barrier) ? barrier : RememberedSymbol;
        }

        if (SymbolForBarrier(state, cell, out var visibleBarrier))
        {
            return visibleBarrier;
        }

        if (cell == state.Lure)
        {
            return LureSymbol;
        }

        if (cell == state.Enemy)
        {
            return EnemySymbol;
        }

        if (state.Keys.Contains(cell))
        {
            return KeySymbol;
        }

        return cell == state.Exit ? ExitSymbol : EmptySymbol;
    }
}
