using MazeGame.Core.Maze;

namespace MazeGame.Core.Game;

/// <summary>
/// One hunter as a frame sees it: the cell it is on, the cell it stepped from, how far through
/// that step it is, and whether it is stunned.
/// </summary>
/// <param name="Cell">The hunter's current cell.</param>
/// <param name="From">The cell it stepped from, for the renderer's slide.</param>
/// <param name="StepFraction">How far through its step it is, from 0 to 1; 1 means standing still.</param>
/// <param name="Stunned">Whether it is held by a catch or a wisp.</param>
/// <param name="Dormant">
/// Whether it is asleep under a floor rule: a sleeper neither moves nor carries light until it
/// wakes, so a renderer draws nothing where it stands.
/// </param>
/// <remarks>
/// A value type on purpose: a renderer reads the whole list every frame, and a snapshot of three
/// hunters should not be three allocations. The fields are the minimum a screen needs to draw a
/// hunter, so no renderer ever reaches into <see cref="GameState"/>'s private state.
/// </remarks>
public readonly record struct HunterView(
    Cell Cell,
    Cell From,
    double StepFraction,
    bool Stunned,
    bool Dormant = false);
