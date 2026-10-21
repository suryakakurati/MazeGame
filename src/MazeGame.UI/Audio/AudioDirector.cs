using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using MazeGame.Core.Game;
using Raylib_cs;

namespace MazeGame.UI.Audio;

/// <summary>
/// Owns the audio device and feeds it from <see cref="AudioEngine"/> on the device's own thread.
/// </summary>
/// <remarks>
/// <para>
/// A window with no sound card (CI, a headless machine, a broken driver) must still be a playable
/// game, so <see cref="TryCreate"/> returns <see langword="null"/> when no device opens and every
/// caller uses <c>?.</c>. Nothing in Core knows this type exists.
/// </para>
/// <para>
/// The stream is fed by a device callback rather than by polling from the game loop: Raylib's
/// <c>UpdateAudioStream</c> refuses a write when the ring buffer is full, and polling for room
/// would either stutter or add a buffer's worth of input latency. The callback runs on miniaudio's
/// thread and mixes straight into the buffer it is handed, so a footstep lands within one device
/// period (~46 ms) of the key press.
/// </para>
/// <para>
/// A floor's score is synthesised on a worker and adopted on the first frame of play, so the
/// mixer never waits for a track and the game loop never stalls to arrange one. Until the new
/// track is ready the previous floor's score keeps playing — silence between floors reads as a
/// bug, a track that arrives a beat late does not.
/// </para>
/// </remarks>
internal sealed class AudioDirector : IDisposable
{
    /// <summary>The device period: 2048 frames, about 46 ms at 44.1 kHz.</summary>
    private const int BlockFrames = 2048;

    /// <summary>How long the last floor plays without its rhythm and lead.</summary>
    private const double LastFloorCalmSeconds = 10.0;

    /// <summary>
    /// The live director, for the device callback.
    /// </summary>
    /// <remarks>
    /// A callback in Raylib is a plain function pointer, so it cannot close over an instance. There
    /// is exactly one audio device in the process, which makes a single static the honest shape;
    /// clearing it on dispose is what makes a late callback harmless.
    /// </remarks>
    private static AudioDirector? _current;

    private readonly AudioEngine _engine = new();
    private readonly AudioStream _stream;

    private volatile MusicIntensity _intensity = MusicIntensity.Calm;
    private Task<(ScoreStems Score, double CalmSeconds)>? _render;
    private long _duckUntil;
    private bool _disposed;
    private int _step;
    private double _heartbeatLeft;

    /// <summary>Wraps an opened stream; <see cref="TryCreate"/> installs the callback.</summary>
    /// <param name="stream">A valid audio stream.</param>
    private AudioDirector(AudioStream stream) => _stream = stream;

    /// <summary>
    /// Opens the audio device, installs the mixer and starts the stream.
    /// </summary>
    /// <returns>
    /// The director, or <see langword="null"/> when there is no device to play through.
    /// </returns>
    public static unsafe AudioDirector? TryCreate()
    {
        Raylib.SetAudioStreamBufferSizeDefault(BlockFrames);
        Raylib.InitAudioDevice();

        if (!Raylib.IsAudioDeviceReady())
        {
            return null;
        }

        // The bank is a static table of twenty-odd buffers; building it here keeps that work off
        // the audio thread, where it would be heard as a gap before the first footstep.
        _ = SfxBank.Ids.Count;

        var stream = Raylib.LoadAudioStream((uint)Synth.SampleRate, 16, AudioEngine.Channels);

        if (!Raylib.IsAudioStreamValid(stream))
        {
            Raylib.CloseAudioDevice();
            return null;
        }

        var director = new AudioDirector(stream);

        Raylib.SetAudioStreamCallback(stream, &OnAudio);
        _current = director;
        Raylib.PlayAudioStream(stream);

        return director;
    }

    /// <summary>
    /// Arranges the track for a floor; it starts when the render finishes.
    /// </summary>
    /// <param name="themeId">The floor's theme id, which is also its atlas id.</param>
    /// <param name="floorNumber">The floor's number, which salts the humanisation.</param>
    /// <param name="seed">The run seed, so the same run sounds the same.</param>
    /// <remarks>
    /// Arranging a floor is 200-400 ms of synthesis and a few megabytes of arrays. Doing that on
    /// the game thread was both a visible stall on the intro card and, through the GC pauses it
    /// caused, a way for the audio callback to miss a deadline — which is heard as a burst of
    /// static. The previous track keeps playing while the next one is arranged on a worker.
    /// </remarks>
    public void PlayFloor(string themeId, int floorNumber, int seed)
    {
        var theme = MusicCatalog.For(themeId);
        var calm = floorNumber >= MusicCatalog.All.Count ? LastFloorCalmSeconds : 0;

        _render = Task.Run(() => (ScoreRenderer.Render(theme, floorNumber, seed), calm));
    }

    /// <summary>Stops the music, for a return to the title.</summary>
    public void StopMusic()
    {
        _render = null;
        _engine.SetScore(null);
    }

    /// <summary>
    /// Starts the floor's track once its render has finished, without blocking a frame.
    /// </summary>
    /// <remarks>
    /// A failed render is dropped rather than thrown on the game loop: a floor with no music is a
    /// silent floor, not a crashed run, and the previous score simply keeps playing.
    /// </remarks>
    private void AdoptRenderedScore()
    {
        if (_render is not { IsCompleted: true } render)
        {
            return;
        }

        _render = null;

        if (!render.IsCompletedSuccessfully)
        {
            return;
        }

        var (score, calm) = render.Result;

        _engine.SetScore(score);
        _intensity = MusicIntensity.Calm;
        _engine.HoldCalm(calm);
    }

    /// <summary>Asks the music to move to <paramref name="intensity"/> on the next bar.</summary>
    /// <param name="intensity">The intensity to move to.</param>
    public void SetIntensity(MusicIntensity intensity) => _intensity = intensity;

    /// <summary>Follows the floor: intensity and the last-heart heartbeat.</summary>
    /// <param name="state">The floor being played.</param>
    /// <param name="deltaSeconds">The frame's length.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="state"/> is null.</exception>
    public void Update(GameState state, double deltaSeconds)
    {
        ArgumentNullException.ThrowIfNull(state);

        AdoptRenderedScore();

        _intensity = MusicMix.From(state);

        var lastHeart = state.Hearts <= 1 && state.Status == GameStatus.InProgress;

        if (!lastHeart)
        {
            _heartbeatLeft = 0;
            return;
        }

        // The heartbeat is the one sound allowed to outlive the duck: on the last heart it is the
        // floor's actual pulse, not a stinger.
        _heartbeatLeft -= deltaSeconds;

        if (_heartbeatLeft <= 0)
        {
            _engine.Play(SfxId.Heartbeat, 0.5f);
            _heartbeatLeft = 0.92;
        }
    }

    /// <summary>Turns the events a floor raised into sound.</summary>
    /// <param name="events">The events drained from the floor this frame.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="events"/> is null.</exception>
    public void Handle(IReadOnlyList<GameEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        foreach (var gameEvent in events)
        {
            switch (gameEvent)
            {
                case GameEvent.Step:
                    PlayStep();
                    break;

                case GameEvent.KeyPicked:
                    _engine.Play(SfxId.KeyPicked, 0.75f);
                    break;

                case GameEvent.HeartLost:
                    _engine.Play(SfxId.Damage);
                    Duck(1.4);
                    break;

                case GameEvent.HeartLostLast:
                    _engine.Play(SfxId.LastHeart);
                    Duck(2.4);
                    break;

                case GameEvent.KillerStep:
                    _engine.Play(SfxId.KillerStep, 0.4f, 1.0f, -0.15f);
                    break;

                case GameEvent.SealPlaced:
                    _engine.Play(SfxId.SealPlaced, 0.85f);
                    break;

                case GameEvent.SealCracked:
                    _engine.Play(SfxId.SealCracked, 0.5f);
                    break;

                case GameEvent.SealBroken:
                    _engine.Play(SfxId.SealBroken, 0.9f);
                    break;

                case GameEvent.WispLit:
                    _engine.Play(SfxId.WispLit, 0.7f, 1.0f, 0.2f);
                    break;

                case GameEvent.WispConsumed:
                    _engine.Play(SfxId.WispConsumed, 0.7f);
                    break;

                case GameEvent.Blink:
                    _engine.Play(SfxId.NearMiss, 0.7f, 1.18f, 0.2f);
                    break;

                case GameEvent.Veiled:
                    _engine.Play(SfxId.WispLit, 0.45f, 0.82f, -0.1f);
                    break;

                case GameEvent.Drag:
                    _engine.Play(SfxId.LungeTell, 0.7f, 0.72f);
                    break;

                case GameEvent.LanternThrown:
                    _engine.Play(SfxId.WispLit, 0.65f, 1.0f, 0.3f);
                    break;

                case GameEvent.LanternReturned:
                    _engine.Play(SfxId.WispConsumed, 0.5f, 1.1f);
                    break;

                case GameEvent.Shoved:
                    _engine.Play(SfxId.SealBroken, 0.85f, 1.12f);
                    break;

                case GameEvent.Graced:
                    _engine.Play(SfxId.Heartbeat, 0.7f, 0.9f);
                    break;

                case GameEvent.Revived:
                    _engine.Play(SfxId.FloorCleared, 0.6f, 0.8f);
                    break;

                case GameEvent.WallSealed:
                case GameEvent.WallClosed:
                    _engine.Play(SfxId.SealPlaced, 0.7f, 0.82f);
                    break;

                case GameEvent.WallOpened:
                    _engine.Play(SfxId.SealBroken, 0.55f, 0.86f);
                    break;

                case GameEvent.ExitMoved:
                    _engine.Play(SfxId.KeyPulse, 0.7f, 0.8f);
                    break;

                case GameEvent.EclipseFell:
                    _engine.Play(SfxId.LungeTell, 0.55f, 0.65f);
                    break;

                case GameEvent.EclipseLifted:
                    _engine.Play(SfxId.WispLit, 0.4f, 1.2f);
                    break;

                case GameEvent.FloorCleared:
                    _engine.Play(SfxId.FloorCleared, 0.8f);
                    break;

                case GameEvent.RunLost:
                    _engine.Play(SfxId.RunLost, 0.9f);
                    break;

                default:
                    break;
            }
        }
    }

    /// <summary>Plays the paper flick of a card under the cursor.</summary>
    public void PlayCardHover() => _engine.Play(SfxId.CardHover, 0.5f);

    /// <summary>Plays the stamp of a card taken, or of a draft skipped.</summary>
    /// <param name="taken">True when a card was taken, false when the draft was skipped.</param>
    public void PlayCardResolved(bool taken) =>
        _engine.Play(SfxId.CardTake, 0.8f, taken ? 1.0f : 0.86f);

    /// <summary>Ducks the music for a stinger.</summary>
    /// <param name="seconds">How long the duck lasts.</param>
    public void Duck(double seconds)
    {
        var until = Stopwatch.GetTimestamp() + (long)(seconds * Stopwatch.Frequency);
        Interlocked.Exchange(ref _duckUntil, until);
    }

    /// <summary>Stops the stream, then releases it and the device.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _current = null;
        Raylib.StopAudioStream(_stream);
        Raylib.UnloadAudioStream(_stream);
        Raylib.CloseAudioDevice();
    }

    /// <summary>
    /// The device callback: fills the buffer Raylib hands over.
    /// </summary>
    /// <param name="buffer">Interleaved 16-bit stereo samples to write.</param>
    /// <param name="frames">How many frames the device wants.</param>
    /// <remarks>
    /// This runs on the audio thread and must not throw: the engine's maths is allocation-free and
    /// bounds-checked, and everything it needs was built before the stream started.
    /// </remarks>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static unsafe void OnAudio(void* buffer, uint frames)
    {
        var director = _current;

        if (director is null || buffer is null)
        {
            // Never leave the device's buffer untouched: a callback that returns early hands the
            // speaker whatever was in that memory, which is heard as a burst of static.
            if (buffer is not null)
            {
                new Span<byte>(buffer, (int)frames * AudioEngine.Channels * sizeof(short)).Clear();
            }

            return;
        }

        // The mix parameters are read once per block, so a change made on the game thread is
        // heard on the next buffer rather than fighting the one being written.
        director._engine.SetIntensity(director._intensity);
        director._engine.Duck(SecondsLeft(Interlocked.Read(ref director._duckUntil)));

        var samples = new Span<short>(buffer, (int)frames * AudioEngine.Channels);
        director._engine.Render(samples);
    }

    /// <summary>Converts a timestamp into seconds still to wait.</summary>
    private static double SecondsLeft(long until)
    {
        var remaining = until - Stopwatch.GetTimestamp();
        return remaining <= 0 ? 0 : remaining / (double)Stopwatch.Frequency;
    }

    /// <summary>Plays a footstep, cycling the three variants and nudging the pitch.</summary>
    private void PlayStep()
    {
        _step = (_step + 1) % 3;

        var id = _step switch
        {
            0 => SfxId.PlayerStep1,
            1 => SfxId.PlayerStep2,
            _ => SfxId.PlayerStep3,
        };

        _engine.Play(id, 0.3f, ((_step - 1) * 0.02f) + 1.0f);
    }
}
