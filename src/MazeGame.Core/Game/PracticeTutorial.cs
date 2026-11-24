using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using MazeGame.Core.Abstractions;
using MazeGame.Core.Maze;
using MazeGame.Core.Pathfinding;

namespace MazeGame.Core.Game;

/// <summary>
/// The guided practice floor behind the tutorial: a small, safe chamber that teaches walking,
/// blinking, the blooms, sprinting and the arch by asking for each one in turn.
/// </summary>
/// <remarks>
/// <para>
/// This is the tutorial's "do it" half. The pages in <see cref="Tutorial"/> say what everything
/// is; this floor makes the player walk, blink through a wall, pick up the blooms, sprint and
/// open the arch on a floor where nothing can hurt them — one hunter, and a clock slow enough
/// that it never takes a step.
/// </para>
/// <para>
/// The steps are derived from the floor rather than counted by hand: "walked" is the player no
/// longer standing on the cell they started on, "blinked" is a spent blink charge, "picked a
/// bloom" is <see cref="GameState.KeysCollected"/>, "sprinted" is a few steps taken while
/// <see cref="GameState.IsSprinting"/>, and "opened the arch" is the arch's own rule. That is why
/// one action can pass two steps, and why a frontend only has to call <see cref="Update"/> and
/// read <see cref="Prompt"/>.
/// </para>
/// <para>
/// The chamber is laid out the way the game places things — player first, then the arch, then the
/// blooms — so the arch is right beside the start and stays locked until the blooms are picked.
/// A short wall stands between the arch and the first bloom: the player walks onto the arch,
/// blinks through the wall straight onto a bloom, takes the second, sprints a few steps and walks
/// back, which is the whole loop of the real game in under a minute.
/// </para>
/// <para>
/// It is a room rather than a corridor on purpose: the window draws a floor through a viewport
/// sized by the lantern, so a one-row lesson would be a thin line lost in a dark canvas. A room
/// gives the lesson the same frame a real floor has.
/// </para>
/// </remarks>
public sealed class PracticeTutorial
{
    /// <summary>How many cells across the teaching chamber is.</summary>
    public const int ChamberSize = 9;

    /// <summary>How long each step of the practice floor takes, in seconds.</summary>
    private const double StepSeconds = 0.18;

    /// <summary>How slow the practice hunter's clock is, in seconds per step.</summary>
    /// <remarks>
    /// It is on the floor because a floor must have a hunter, and it never reaches the player: ten
    /// minutes a step means no tick a tutorial can last will move it.
    /// </remarks>
    private const double HunterStepSeconds = 600.0;

    /// <summary>How far the practice lantern reaches, in steps.</summary>
    /// <remarks>
    /// Wide enough that most of the chamber is lit at once, so the two blooms and the arch can be
    /// seen from the start: the lesson never asks the player to search.
    /// </remarks>
    private const int LanternRadius = 6;

    /// <summary>How many sprinting steps the sprint lesson asks for.</summary>
    public const int SprintStepsNeeded = 3;

    /// <summary>How many blinks the practice floor hands out: one for the lesson, one spare.</summary>
    private const int PracticeBlinks = 2;

    private static readonly string[] Prompts =
    [
        "WALK EAST ONTO THE ARCH: A TAP IS ONE TILE. HOLD A KEY TO KEEP WALKING.",
        "A WALL BLOCKS THE WAY. FACE IT AND PRESS SPACE TO BLINK THROUGH.",
        "STEP ONTO A BLOOM - THE FLOWER RINGED IN GOLD - TO PICK IT UP.",
        "TAKE THE OTHER BLOOM: THE ARCH UNLOCKS WHEN THE COUNT REACHES ZERO.",
        "HOLD SHIFT WHILE YOU WALK TO SPRINT. TAKE A FEW QUICK STEPS.",
        "NOW STAND ON THE ARCH. THE WAY DOWN OPENS.",
    ];

    /// <summary>What the floor says once the walk is done.</summary>
    public const string FinishPrompt = "THE ARCH IS OPEN. THAT IS THE WHOLE LOOP: PICK, OPEN, DESCEND.";

    private readonly Grid _maze;
    private readonly Cell _start;

    // The cell the player stood on at the last Update, so a new step is noticed once.
    private Cell _lastSeen;

    // Steps taken while sprinting. Sprint is a hold, so it is counted as it happens rather than
    // read back afterwards: the flag drops the moment Shift is let go.
    private int _sprintSteps;

    /// <summary>Creates the practice floor at its first step.</summary>
    public PracticeTutorial()
    {
        _maze = Chamber(ChamberSize);

        Definition = new FloorDefinition
        {
            Number = 1,
            Name = "The Threshold",
            ThemeId = "shallows",
            Rows = ChamberSize,
            Columns = ChamberSize,
            KeyCount = 2,
            HunterCount = 1,
            EnemyStepSeconds = HunterStepSeconds,
            PlayerStepSeconds = StepSeconds,
            VisionRadius = LanternRadius,
            EnemyAuraRadius = 0,
            EnemyAuraDistance = 0,
            Powerups = PowerupCharges.None with { Blink = PracticeBlinks },
            MaxHearts = 3,
        };

        Floor = new GameState(
            new GameConfig { Seed = null }.ForFloor(Definition, startingHearts: 3),
            _maze,
            new DijkstraPathfinder(),
            new LaidOutInOrder());

        _start = Floor.Player;
        _lastSeen = _start;
    }

    /// <summary>
    /// Gets the practice floor's definition: its name, theme and size, for the frontend's chrome.
    /// </summary>
    public FloorDefinition Definition { get; }

    /// <summary>Gets the practice floor itself, played by the frontend.</summary>
    public GameState Floor { get; }

    /// <summary>Gets how many steps the player has finished, from zero to <see cref="StepCount"/>.</summary>
    public int CompletedSteps { get; private set; }

    /// <summary>Gets how many steps the practice floor asks for.</summary>
    public static int StepCount => Prompts.Length;

    /// <summary>Gets a value indicating whether the arch has been opened.</summary>
    public bool IsComplete => CompletedSteps >= Prompts.Length;

    /// <summary>Gets what the floor is asking for right now.</summary>
    public string Prompt => IsComplete ? FinishPrompt : Prompts[CompletedSteps];

    /// <summary>Gets the floor's steps, in order, as a read-only list.</summary>
    public static IReadOnlyList<string> Steps { get; } =
        new ReadOnlyCollection<string>(Prompts);

    /// <summary>
    /// Advances past every step the floor already satisfies.
    /// </summary>
    /// <remarks>
    /// Call it after every input and every tick: a single walk can satisfy two steps, and the
    /// player should never be told to do a thing they have already done.
    /// </remarks>
    public void Update()
    {
        if (!SameCell(Floor.Player, _lastSeen))
        {
            _lastSeen = Floor.Player;

            if (Floor.IsSprinting)
            {
                _sprintSteps++;
            }
        }

        // The arch ends the floor, and a finished floor cannot be asked for anything more: a
        // player who skipped a lesson has still walked the loop, so the practice is done.
        if (Floor.Status == GameStatus.Won)
        {
            CompletedSteps = Prompts.Length;
            return;
        }

        while (!IsComplete && Satisfied(CompletedSteps))
        {
            CompletedSteps++;
        }
    }

    /// <summary>Tests whether step <paramref name="step"/> has been done on the floor.</summary>
    private bool Satisfied(int step) => step switch
    {
        0 => !SameCell(Floor.Player, _start),
        1 => Floor.PowerupsRemaining.Blink < PracticeBlinks,
        2 => Floor.KeysCollected >= 1,
        3 => Floor.KeysRemaining == 0,
        4 => _sprintSteps >= SprintStepsNeeded,
        _ => Floor.Status == GameStatus.Won,
    };

    private static bool SameCell(Cell left, Cell right) =>
        left.Row == right.Row && left.Column == right.Column;

    /// <summary>
    /// A random source that does not shuffle, so the lesson's cells are taken in reading order:
    /// the player in the corner, the arch beside them, the blooms past it and the hunter in the
    /// far corner.
    /// </summary>
    /// <remarks>
    /// This is the one floor in the game that is not generated, so it is also the one floor whose
    /// placement must not be rolled. Every other use of <see cref="IRandom"/> in the game still
    /// goes through <see cref="SeededRandom"/>.
    /// </remarks>
    private sealed class LaidOutInOrder : IRandom
    {
        /// <inheritdoc />
        public int NextInt(int maxExclusive) => 0;

        /// <inheritdoc />
        /// <remarks>The list is left exactly as it was given.</remarks>
        public void Shuffle<T>(IList<T> items)
        {
            _ = items;
        }
    }

    /// <summary>Builds a square chamber <paramref name="size"/> cells across.</summary>
    /// <remarks>
    /// Every cell starts walled, and only the walls between two cells of the chamber are taken
    /// out, so the room arrives with the grid's own boundary as its rock without any of the maze's
    /// machinery. Two walls are left standing east of the arch's column, in the top two rows: the
    /// wall the blink lesson asks the player to pass through. The room stays open around it, so
    /// the floor is still one connected space.
    /// </remarks>
    private static Grid Chamber(int size)
    {
        var grid = new Grid(size, size);

        for (var row = 0; row < size; row++)
        {
            for (var column = 0; column < size; column++)
            {
                if (column < size - 1 && !IsBlinkWall(row, column))
                {
                    grid.RemoveWall(row, column, Direction.East);
                }

                if (row < size - 1)
                {
                    grid.RemoveWall(row, column, Direction.South);
                }
            }
        }

        return grid;
    }

    /// <summary>
    /// Returns whether the east wall of a cell is the one the blink lesson keeps: between the
    /// arch's column and the blooms', in the top two rows.
    /// </summary>
    private static bool IsBlinkWall(int row, int column) => column == 1 && row <= 1;
}

