namespace MazeGame.UI.Audio;

/// <summary>
/// Every one-shot sound the game can make.
/// </summary>
/// <remarks>
/// The names describe the moment, not the waveform, because that is how the frontend chooses one:
/// a <see cref="MazeGame.Core.Game.GameEvent"/> maps to an id, and the recipe behind the id is
/// free to change without touching a single rule.
/// </remarks>
public enum SfxId
{
    /// <summary>A boot on wet stone, variant one.</summary>
    PlayerStep1,

    /// <summary>A boot on wet stone, variant two.</summary>
    PlayerStep2,

    /// <summary>A boot on wet stone, variant three.</summary>
    PlayerStep3,

    /// <summary>The killer's step: a low thump with a wet tail.</summary>
    KillerStep,

    /// <summary>A catch that cost a heart.</summary>
    Damage,

    /// <summary>The catch that took the last heart.</summary>
    LastHeart,

    /// <summary>Two thumps, played once per beat while the player is on their last heart.</summary>
    Heartbeat,

    /// <summary>A key lifting off the floor: a rising three-note bell.</summary>
    KeyPicked,

    /// <summary>The sonar ping of a key the run can still find.</summary>
    KeyPulse,

    /// <summary>A passage closing: a dry seal, then a heavy settle.</summary>
    WallSealed,

    /// <summary>Stone breaking apart: two cracks and a body blow.</summary>
    WallBroken,

    /// <summary>Something glowing into life: airy noise under a rising tone.</summary>
    SoftLight,

    /// <summary>A light guttering out: a falling tone with the air going out of it.</summary>
    LightOut,

    /// <summary>A locked arch: dull, immovable.</summary>
    ArchLocked,

    /// <summary>The arch opening because every key is in.</summary>
    ArchUnlocked,

    /// <summary>A floor cleared: a short chord that resolves.</summary>
    FloorCleared,

    /// <summary>A card under the cursor.</summary>
    CardHover,

    /// <summary>A card taken.</summary>
    CardTake,

    /// <summary>The run ending: the motif, cut off.</summary>
    RunLost,

    /// <summary>The killer drawing breath before it lunges.</summary>
    LungeTell,

    /// <summary>Something large passing very close.</summary>
    NearMiss,
}
