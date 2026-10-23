using System;
using System.IO;
using MazeGame.Core.Game;
using MazeGame.UI.Persistence;
using Xunit;

namespace MazeGame.Tests.Persistence;

/// <summary>
/// Verifies the two files the game keeps between sessions: the run and the garden.
/// </summary>
/// <remarks>
/// Each test gets its own folder, so nothing here can read or write a player's real save. The
/// interesting rules are that a save round-trips, that the garden accumulates across runs, and that
/// an unreadable file is quarantined rather than crashing the frontend.
/// </remarks>
public sealed class SaveStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "mazegame-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void AFreshStore_HasNoRunAndAnEmptyGarden()
    {
        var store = new SaveStore(_directory);

        Assert.False(store.HasRun);
        Assert.Null(store.LoadRun());
        Assert.Equal(GardenProfile.Empty, store.LoadGarden());
    }

    [Fact]
    public void ARun_RoundTripsThroughTheFile()
    {
        var store = new SaveStore(_directory);
        var snapshot = new RunSnapshot
        {
            RunSeed = 4242,
            FloorIndex = 3,
            Hearts = 2,
            CarryHearts = 1,
            BloomsBanked = 7,
            CardIds = new[] { "wide-eyes", "quick-hands" },
            Conditions = new[] { DescentCondition.Mute },
            ChosenGates = new[] { 8 },
        };

        store.SaveRun(snapshot);

        Assert.True(store.HasRun);

        var loaded = store.LoadRun();

        Assert.NotNull(loaded);
        Assert.Equal(snapshot.RunSeed, loaded.RunSeed);
        Assert.Equal(snapshot.FloorIndex, loaded.FloorIndex);
        Assert.Equal(snapshot.Hearts, loaded.Hearts);
        Assert.Equal(snapshot.CarryHearts, loaded.CarryHearts);
        Assert.Equal(snapshot.BloomsBanked, loaded.BloomsBanked);
        Assert.Equal(snapshot.CardIds, loaded.CardIds);
        Assert.Equal(snapshot.Conditions, loaded.Conditions);
        Assert.Equal(snapshot.ChosenGates, loaded.ChosenGates);
    }

    [Fact]
    public void ClearingTheRun_LeavesTheGardenAlone()
    {
        var store = new SaveStore(_directory);

        store.SaveRun(new RunSnapshot { RunSeed = 1, Hearts = 3 });
        store.RecordRun(blooms: 4, floorReached: 2);
        store.ClearRun();

        Assert.False(store.HasRun);
        Assert.Equal(4, store.LoadGarden().Blooms);
    }

    [Fact]
    public void TheGarden_AccumulatesAcrossRunsAndKeepsTheDeepestFloor()
    {
        var store = new SaveStore(_directory);

        store.RecordRun(blooms: 4, floorReached: 2);
        store.RecordRun(blooms: 6, floorReached: 9);
        var garden = store.RecordRun(blooms: 0, floorReached: 1);

        Assert.Equal(10, garden.Blooms);
        Assert.Equal(9, garden.DeepestFloor);
        Assert.Equal(3, garden.Runs);
    }

    [Fact]
    public void AnUnreadableRun_IsQuarantinedAndReportedAsAbsent()
    {
        var store = new SaveStore(_directory);
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "run.json"), "{ this is not json");

        Assert.Null(store.LoadRun());

        Assert.False(store.HasRun);
        Assert.True(File.Exists(Path.Combine(_directory, "run.json.bad")));
    }

    [Fact]
    public void RecordingANegativeRun_IsRefused()
    {
        var store = new SaveStore(_directory);

        Assert.Throws<ArgumentOutOfRangeException>(() => store.RecordRun(blooms: -1, floorReached: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => store.RecordRun(blooms: 1, floorReached: 0));
    }
}
