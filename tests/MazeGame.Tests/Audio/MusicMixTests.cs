using MazeGame.Core.Game;
using MazeGame.Core.Maze;
using MazeGame.Tests.TestDoubles;
using MazeGame.UI.Audio;

namespace MazeGame.Tests.Audio;

/// <summary>
/// Covers how the music decides to change: distance thresholds, and the states where a change
/// would be a lie (aiming, a stunned killer, a finished floor).
/// </summary>
public sealed class MusicMixTests
{
    [Fact]
    public void Calm_KeepsTheBedAndNothingElse()
    {
        var gains = MusicMix.Gains(MusicIntensity.Calm);

        Assert.Equal(1.0f, gains.Bed);
        Assert.Equal(0.0f, gains.Rhythm);
        Assert.Equal(0.0f, gains.Lead);
    }

    [Fact]
    public void TheArrangement_OpensUpAsTheKiller_ClosesIn()
    {
        var calm = MusicMix.Gains(MusicIntensity.Calm);
        var tense = MusicMix.Gains(MusicIntensity.Tense);
        var chase = MusicMix.Gains(MusicIntensity.Chase);

        Assert.True(tense.Rhythm > calm.Rhythm);
        Assert.True(chase.Rhythm > tense.Rhythm);
        Assert.True(chase.Lead > tense.Lead);
        Assert.Equal(calm.Bed, chase.Bed);
    }

    [Fact]
    public void Damage_DucksEveryStem()
    {
        var hurt = MusicMix.Gains(MusicIntensity.Hurt);

        Assert.True(hurt.Bed < MusicMix.Gains(MusicIntensity.Calm).Bed);
        Assert.True(hurt.Rhythm < MusicMix.Gains(MusicIntensity.Chase).Rhythm);
        Assert.True(hurt.Lead < MusicMix.Gains(MusicIntensity.Chase).Lead);
    }

    [Theory]
    [InlineData(0, MusicIntensity.Chase)]
    [InlineData(3, MusicIntensity.Chase)]
    [InlineData(4, MusicIntensity.Tense)]
    [InlineData(7, MusicIntensity.Tense)]
    [InlineData(8, MusicIntensity.Calm)]
    [InlineData(20, MusicIntensity.Calm)]
    public void Distance_ChoosesTheIntensity(int distance, MusicIntensity expected) =>
        Assert.Equal(expected, MusicMix.ForDistance(distance));

    [Fact]
    public void ANegativeDistance_IsRefused() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => MusicMix.ForDistance(-1));

    [Fact]
    public void AFreshFloor_IsTenseRatherThanChased()
    {
        // The test corridor is six cells long: the killer starts five away, which is background
        // noise, not a chase.
        var state = TestLevels.CorridorGame();

        Assert.Equal(MusicIntensity.Tense, MusicMix.From(state));
    }

    [Fact]
    public void Aiming_SettlesTheMusic()
    {
        var state = TestLevels.CorridorGame();

        Assert.True(state.BeginBarrierPlacement());

        Assert.Equal(MusicIntensity.Calm, MusicMix.From(state));
    }

    [Fact]
    public void ACatch_LeavesTheKillerStunned_SoTheMusicSettles()
    {
        var state = TestLevels.CorridorGame(startingHearts: 2, enemyStepSeconds: 0.2);

        for (var tick = 0; tick < 12 && state.Hearts > 1; tick++)
        {
            state.Tick(0.2);
        }

        Assert.True(state.IsEnemyStunned);
        Assert.Equal(GameStatus.InProgress, state.Status);
        Assert.Equal(MusicIntensity.Calm, MusicMix.From(state));
    }

    [Fact]
    public void AFinishedFloor_HasNoIntensityToChase()
    {
        var state = TestLevels.CorridorGame(keyCount: 0);

        Assert.True(state.TryMove(Direction.East));
        Assert.Equal(GameStatus.Won, state.Status);

        Assert.Equal(MusicIntensity.Calm, MusicMix.From(state));
    }
}
