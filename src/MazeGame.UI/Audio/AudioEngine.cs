using System;
using System.Collections.Concurrent;

namespace MazeGame.UI.Audio;

/// <summary>
/// The mixer: three music stems, sixteen one-shot voices, a duck and a master conversion to PCM.
/// </summary>
/// <remarks>
/// <para>
/// The engine knows nothing about Raylib, so the test suite can render a block and inspect it.
/// The device wrapper (<see cref="AudioDirector"/>) is the only thing that opens a speaker.
/// </para>
/// <para>
/// Mix changes are applied on a bar line rather than on the frame the state changed: the chase
/// hatches with the music instead of on top of it, which is the difference between "the game
/// reacted" and "the soundtrack noticed".
/// </para>
/// </remarks>
public sealed class AudioEngine
{
    /// <summary>How many output channels the engine writes.</summary>
    public const int Channels = 2;

    /// <summary>How many one-shots can sound at once.</summary>
    public const int MaxVoices = 16;

    /// <summary>The largest block the engine mixes at once, in frames.</summary>
    /// <remarks>
    /// The device asks for a block of its own size; a bigger request is mixed in chunks of this
    /// size into a buffer allocated once, so the render path never allocates (and so never wakes
    /// the garbage collector in the middle of a sound).
    /// </remarks>
    public const int MaxBlockFrames = 4096;

    /// <summary>The music's headroom under the effects.</summary>
    private const float MusicGain = 0.72f;

    /// <summary>The gain applied to a one-shot's recipe.</summary>
    private const float EffectGain = 0.9f;

    /// <summary>How far the music ducks under a damage stinger.</summary>
    private const float DuckDepth = 0.35f;

    /// <summary>How long a mix change takes to arrive, in seconds.</summary>
    /// <remarks>
    /// A gain jump on a bar line is still a step in the waveform, and a stem that is already
    /// sounding would click. Four hundred milliseconds is long enough to be inaudible and short
    /// enough to still read as "this bar".
    /// </remarks>
    private const double RampSeconds = 0.4;

    private readonly Voice[] _voices = new Voice[MaxVoices];
    private readonly ConcurrentQueue<PlayRequest> _requests = new();

    private readonly float[] _mix = new float[MaxBlockFrames * Channels];
    private ScoreStems? _score;
    private ScoreStems? _adopted;
    private MusicIntensity _target = MusicIntensity.Calm;
    private MusicIntensity _active = MusicIntensity.Calm;
    private StemGains _gains = MusicMix.Gains(MusicIntensity.Calm);
    private StemGains _appliedGains = MusicMix.Gains(MusicIntensity.Calm);
    private double _calmHoldSeconds;
    private int _position;
    private int _bar = -1;
    private float _duck = 1.0f;
    private double _duckSeconds;
    private int _nextVoice;

    /// <summary>Creates a silent engine; nothing sounds until a score or an effect is given to it.</summary>
    public AudioEngine()
    {
        for (var index = 0; index < _voices.Length; index++)
        {
            _voices[index] = new Voice();
        }
    }

    /// <summary>Gets the score currently playing, or null when the music is silent.</summary>
    public ScoreStems? Score => Volatile.Read(ref _score);

    /// <summary>Gets the intensity actually being mixed, which lags the target to the bar line.</summary>
    public MusicIntensity ActiveIntensity => _active;

    /// <summary>Gets the intensity the game asked for.</summary>
    public MusicIntensity TargetIntensity => _target;

    /// <summary>Gets the current duck gain, for tests and debugging.</summary>
    public float DuckGain => _duck;

    /// <summary>Gets how many blocks have been mixed, which proves a device is draining them.</summary>
    public long RenderedBlocks { get; private set; }

    /// <summary>Gets how many frames have been mixed, in total.</summary>
    public long RenderedFrames { get; private set; }

    /// <summary>
    /// Starts playing <paramref name="score"/>, from the top.
    /// </summary>
    /// <param name="score">The looped stems, or null to stop the music.</param>
    /// <remarks>
    /// The swap is published here but <b>adopted on the audio thread</b>, at the top of the next
    /// block. Resetting the play position from the game thread raced the mixer that was reading
    /// it: the loop could jump forward or back mid-block, which is a click at best. One writer, one
    /// reader, adopted between blocks.
    /// </remarks>
    public void SetScore(ScoreStems? score) => Volatile.Write(ref _score, score);

    /// <summary>Asks for <paramref name="intensity"/>; it takes effect on the next bar.</summary>
    /// <param name="intensity">The intensity to move to.</param>
    public void SetIntensity(MusicIntensity intensity) => _target = intensity;

    /// <summary>Gets how many seconds of calm are still being enforced.</summary>
    public double CalmHoldSeconds => _calmHoldSeconds;

    /// <summary>
    /// Keeps the arrangement at <see cref="MusicIntensity.Calm"/> for <paramref name="seconds"/>.
    /// </summary>
    /// <param name="seconds">How long to hold, measured in audio time.</param>
    /// <remarks>
    /// The last floor opens without its drums and brings them in after its first ten seconds; that
    /// is an arrangement change the state cannot express, because nothing has changed in the maze.
    /// The hold overrides the intensity the floor asks for, not the target, so the moment it ends
    /// the mix moves to whatever the chase actually is.
    /// </remarks>
    public void HoldCalm(double seconds) => _calmHoldSeconds = Math.Max(0, seconds);

    /// <summary>
    /// Ducks the music for <paramref name="seconds"/>, under a stinger.
    /// </summary>
    /// <param name="seconds">How long the duck should last; longer requests win.</param>
    public void Duck(double seconds) => _duckSeconds = Math.Max(_duckSeconds, seconds);

    /// <summary>
    /// Queues a one-shot to start at the next block.
    /// </summary>
    /// <param name="id">The effect to play.</param>
    /// <param name="volume">A gain on top of the recipe's own level.</param>
    /// <param name="pitch">A playback rate; 1 is the recipe as rendered.</param>
    /// <param name="pan">-1 hard left, 0 centre, 1 hard right.</param>
    public void Play(SfxId id, float volume = 1.0f, float pitch = 1.0f, float pan = 0.0f) =>
        _requests.Enqueue(new PlayRequest(id, volume, pitch, pan));

    /// <summary>
    /// Renders <paramref name="output"/> and fills it with interleaved 16-bit stereo samples.
    /// </summary>
    /// <param name="output">
    /// The destination; its length must be frames times <see cref="Channels"/>. A trailing odd
    /// sample is written as silence.
    /// </param>
    /// <remarks>
    /// A block is mixed from the queued effects, the active voices and the music, then converted
    /// once. The float scratch buffer is reused, so a steady stream of audio does not allocate.
    /// </remarks>
    public void Render(Span<short> output)
    {
        var frames = output.Length / Channels;

        if (frames <= 0)
        {
            output.Clear();
            return;
        }

        var offset = 0;

        while (frames > 0)
        {
            var chunk = Math.Min(frames, MaxBlockFrames);
            RenderChunk(output.Slice(offset, chunk * Channels));

            offset += chunk * Channels;
            frames -= chunk;
        }

        if (output.Length % Channels != 0)
        {
            output[^1] = 0;
        }
    }

    /// <summary>Mixes one block of at most <see cref="MaxBlockFrames"/> frames.</summary>
    /// <param name="output">The destination, exactly frames times channels long.</param>
    private void RenderChunk(Span<short> output)
    {
        var frames = output.Length / Channels;
        var blockSeconds = frames / (double)Synth.SampleRate;

        _mix.AsSpan(0, output.Length).Clear();

        DrainRequests();
        MixMusic(frames, blockSeconds);
        MixVoices(frames);

        for (var index = 0; index < output.Length; index++)
        {
            // A soft knee above 0.7 instead of a hard clamp at 1: stacked one-shots over a loud
            // bed used to hit the ceiling and tear into distortion, which reads as static.
            var sample = Limit(_mix[index]);
            output[index] = (short)(Math.Clamp(sample, -1.0f, 1.0f) * 32767.0f);
        }

        _duckSeconds = Math.Max(0, _duckSeconds - blockSeconds);
        _calmHoldSeconds = Math.Max(0, _calmHoldSeconds - blockSeconds);
        RenderedBlocks++;
        RenderedFrames += frames;
    }

    /// <summary>Mixes the three stems for <paramref name="frames"/> samples.</summary>
    private void MixMusic(int frames, double blockSeconds)
    {
        AdoptPublishedScore();

        var score = _adopted;

        if (score is null || score.Frames == 0)
        {
            return;
        }

        // The score can be swapped from another thread while this block is being mixed; a
        // position past the new loop restarts it rather than reading out of bounds.
        if (_position < 0 || _position >= score.Frames)
        {
            _position = 0;
            _bar = -1;
        }

        // One decision per block: if the bar number changed, adopt whatever the game asked for
        // while the last bar was playing.
        if (score.BarFrames > 0 && (_position / score.BarFrames) != _bar)
        {
            _bar = _position / score.BarFrames;
            _active = _calmHoldSeconds > 0 ? MusicIntensity.Calm : _target;
            _gains = MusicMix.Gains(_active);
        }

        var duckTarget = _duckSeconds > 0 ? DuckDepth : 1.0f;
        var step = (float)Math.Min(blockSeconds * 3.0, 1.0);
        _duck += Math.Clamp(duckTarget - _duck, -step, step);

        var from = _appliedGains;
        var to = _gains;
        var ramp = (float)Math.Clamp(blockSeconds / RampSeconds, 0.0, 1.0);

        for (var frame = 0; frame < frames; frame++)
        {
            // The mix moves across the block, so a stem opening on a bar line fades in over 400 ms
            // instead of stepping from silence into the middle of the noise floor.
            var progress = (frame + 1) / (float)frames * ramp;
            var bed = (from.Bed + ((to.Bed - from.Bed) * progress)) * MusicGain * _duck;
            var rhythm = (from.Rhythm + ((to.Rhythm - from.Rhythm) * progress)) * MusicGain * _duck;
            var lead = (from.Lead + ((to.Lead - from.Lead) * progress)) * MusicGain * _duck;

            var index = _position;
            var bedSample = score.Bed[index] * bed;
            var rhythmSample = score.Rhythm[index] * rhythm;
            var leadSample = score.Lead[index] * lead;

            // A little width: the percussion leans left, the motif leans right, the pad stays put.
            _mix[frame * Channels] += bedSample + (rhythmSample * 1.15f) + (leadSample * 0.85f);
            _mix[(frame * Channels) + 1] += bedSample + (rhythmSample * 0.85f) + (leadSample * 1.15f);

            _position++;

            if (_position >= score.Frames)
            {
                _position = 0;
                _bar = -1;
            }
        }

        _appliedGains = new StemGains(
            from.Bed + ((to.Bed - from.Bed) * ramp),
            from.Rhythm + ((to.Rhythm - from.Rhythm) * ramp),
            from.Lead + ((to.Lead - from.Lead) * ramp));
    }

    /// <summary>
    /// Keeps a sample inside [-1, 1], compressing only what goes above 0.7.
    /// </summary>
    /// <param name="sample">The mixed sample.</param>
    /// <returns>The limited sample.</returns>
    /// <remarks>
    /// Quiet audio is untouched, so the mix has the same shape it had; only a collision between a
    /// stinger and the music is bent, and it is bent smoothly rather than clipped flat.
    /// </remarks>
    private static float Limit(float sample)
    {
        var magnitude = MathF.Abs(sample);

        if (magnitude <= 0.7f)
        {
            return sample;
        }

        var bent = 0.7f + (MathF.Tanh((magnitude - 0.7f) * 3.0f) * 0.3f);
        return sample < 0 ? -bent : bent;
    }

    /// <summary>
    /// Adopts a score published by <see cref="SetScore"/>, resetting the loop to its first bar.
    /// </summary>
    /// <remarks>
    /// Called only from the audio thread. The reference comparison is enough because a score is
    /// only ever published once; re-publishing the same object is a no-op on purpose.
    /// </remarks>
    private void AdoptPublishedScore()
    {
        var published = Volatile.Read(ref _score);

        if (ReferenceEquals(published, _adopted))
        {
            return;
        }

        _adopted = published;
        _position = 0;
        _bar = -1;

        // The calm hold is part of the arrangement, so it decides the first bar just as the bar-line
        // rule does later. Reading the target here instead made the last floor open at full chase
        // for one block, which is a burst of drums where the quiet is the point.
        _active = _calmHoldSeconds > 0 ? MusicIntensity.Calm : _target;
        _gains = MusicMix.Gains(_active);

        // _appliedGains is deliberately left alone: it is where the previous track's fade had got
        // to, and starting the new one from there is what keeps a floor change from clicking.
    }

    /// <summary>Mixes every active one-shot voice.</summary>
    private void MixVoices(int frames)
    {
        foreach (var voice in _voices)
        {
            if (!voice.Active)
            {
                continue;
            }

            var samples = voice.Samples;

            for (var frame = 0; frame < frames; frame++)
            {
                var index = (int)voice.Position;

                if (index >= samples.Length - 1)
                {
                    voice.Active = false;
                    break;
                }

                var fraction = (float)(voice.Position - index);
                var sample = samples[index] + ((samples[index + 1] - samples[index]) * fraction);

                _mix[frame * Channels] += sample * voice.Left;
                _mix[(frame * Channels) + 1] += sample * voice.Right;

                voice.Position += voice.Rate;
            }
        }
    }

    /// <summary>Starts the effects queued since the last block, oldest first.</summary>
    private void DrainRequests()
    {
        while (_requests.TryDequeue(out var request))
        {
            var voice = TakeVoice();

            if (voice is null)
            {
                continue;
            }

            var samples = SfxBank.Samples(request.Id);
            var pan = Math.Clamp(request.Pan, -1.0f, 1.0f);
            var angle = (pan + 1.0f) * (MathF.PI / 4.0f);
            var gain = request.Volume * EffectGain;

            voice.Samples = samples;
            voice.Position = 0;
            voice.Rate = Math.Clamp(request.Pitch, 0.25f, 4.0f);
            voice.Left = MathF.Cos(angle) * gain;
            voice.Right = MathF.Sin(angle) * gain;
            voice.Active = true;
        }
    }

    /// <summary>
    /// Finds a voice to use: a free one, or the oldest, so a burst of effects thins itself out
    /// rather than being refused.
    /// </summary>
    private Voice? TakeVoice()
    {
        foreach (var voice in _voices)
        {
            if (!voice.Active)
            {
                return voice;
            }
        }

        var oldest = _voices[_nextVoice];
        _nextVoice = (_nextVoice + 1) % _voices.Length;
        return oldest;
    }

    /// <summary>One queued effect, waiting for the next block.</summary>
    private readonly record struct PlayRequest(SfxId Id, float Volume, float Pitch, float Pan);

    /// <summary>One playing one-shot.</summary>
    private sealed class Voice
    {
        /// <summary>The recipe's samples.</summary>
        public float[] Samples { get; set; } = [];

        /// <summary>The fractional read position, which is what pitch shifts.</summary>
        public double Position { get; set; }

        /// <summary>Samples advanced per output sample.</summary>
        public double Rate { get; set; } = 1.0;

        /// <summary>The gain written to the left channel.</summary>
        public float Left { get; set; }

        /// <summary>The gain written to the right channel.</summary>
        public float Right { get; set; }

        /// <summary>Whether this voice is currently sounding.</summary>
        public bool Active { get; set; }
    }
}
