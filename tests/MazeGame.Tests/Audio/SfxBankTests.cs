using System;
using MazeGame.UI.Audio;

namespace MazeGame.Tests.Audio;

/// <summary>
/// Covers the one-shot bank: every effect exists, is audible, stays inside the ceiling and sounds
/// different from its neighbours. These are the checks that replace "an artist listened to it".
/// </summary>
public sealed class SfxBankTests
{
    [Fact]
    public void TheBank_HoldsEveryEffectExactlyOnce()
    {
        Assert.Equal(Enum.GetValues<SfxId>().Length, SfxBank.Ids.Count);
        Assert.Equal(SfxBank.Ids.Count, SfxBank.Ids.Distinct().Count());
    }

    [Fact]
    public void EveryEffect_IsAudibleAndInsideTheCeiling()
    {
        foreach (var id in SfxBank.Ids)
        {
            var samples = SfxBank.Get(id);

            Assert.True(samples.Length > 0, $"{id} has no samples");

            foreach (var sample in samples)
            {
                Assert.False(float.IsNaN(sample), $"{id} produced NaN");
                Assert.False(float.IsInfinity(sample), $"{id} produced infinity");
            }

            Assert.InRange(Synth.Peak(samples), 0.3f, Synth.PeakCeiling);
            Assert.InRange(Synth.Rms(samples), 0.02, 0.4);
        }
    }

    [Fact]
    public void EveryEffect_FitsInUnderTwoSeconds()
    {
        foreach (var id in SfxBank.Ids)
        {
            var seconds = SfxBank.Get(id).Length / (double)Synth.SampleRate;

            Assert.InRange(seconds, 0.05, 2.0);
        }
    }

    [Fact]
    public void NoTwoEffects_SoundTheSame()
    {
        var signatures = new Dictionary<int, SfxId>();

        foreach (var id in SfxBank.Ids)
        {
            var signature = Signature(SfxBank.Get(id));

            Assert.False(
                signatures.TryGetValue(signature, out var duplicate),
                $"{id} is the same sound as {duplicate}");

            signatures[signature] = id;
        }
    }

    [Fact]
    public void AnUnknownEffect_IsRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SfxBank.Get((SfxId)9999).ToArray());
    }

    /// <summary>Quantises a buffer to a hash, so two recipes cannot pass by being merely similar.</summary>
    private static int Signature(ReadOnlySpan<float> samples)
    {
        var hash = 17;

        foreach (var sample in samples)
        {
            hash = (hash * 31) + (int)Math.Round(sample * 1000, MidpointRounding.AwayFromZero);
        }

        return hash;
    }
}
