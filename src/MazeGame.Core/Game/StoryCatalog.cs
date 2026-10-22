using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace MazeGame.Core.Game;

/// <summary>
/// The gardener's book: one authored page per shipped floor, and the lines the Echo speaks on
/// that floor.
/// </summary>
/// <remarks>
/// <para>
/// This is the one place in the project where text is authored rather than generated. The floors
/// are fixed, numbered one to twenty, and a run is a single sitting, so the descent can be a book
/// with twenty chapters in a fixed order — no grammar, no recombination, and no way for the story
/// to contradict itself on a replay. Twenty pages of three lines is under a thousand words, which
/// is the cheapest story this game will ever get.
/// </para>
/// <para>
/// The catalog is content only: it never reads the run and never changes a rule. The campaign
/// decides which page is on screen and which mood line follows it, and <see cref="GameState"/> owns
/// whether this floor's Echo has been heard.
/// </para>
/// </remarks>
public static class StoryCatalog
{
    /// <summary>How many lines a page may have.</summary>
    /// <remarks>
    /// Three is the limit the card was laid out for. A fourth line would push the garden strip off
    /// the bottom of a short window, and a page that cannot be seen whole is not a page.
    /// </remarks>
    public const int MaxPageLines = 3;

    /// <summary>How many lines the Echo speaks: two of the gardener's voice and the hint.</summary>
    public const int EchoLineCount = 3;

    /// <summary>The longest a single line may be, in characters.</summary>
    /// <remarks>
    /// Set by the card's text column at the minimum window width. The test asserts it, because a
    /// line that runs off the card is invisible on exactly the screens that need the story most.
    /// </remarks>
    public const int MaxLineLength = 100;

    // One entry per shipped floor, in run order. The page is the chapter; the Echo is what the
    // gardener says in the dark three floors after saying nothing; the hint is the floor's rule,
    // taught by the same voice that tells the story, so the tutorial is never a pop-up.
    private static readonly Entry[] Entries =
    [
        new(
            1,
            "ROWS",
            [
                "She planted the first row the week after the funeral.",
                "The water was still warm. She said it was the season.",
                "Every row since has been straight, in case you came back.",
            ],
            ["I keep the rows straight. You always said the crooked ones were mine."],
            "Three blooms open the arch. The roots only want their light back."),
        new(
            2,
            "THE CLOSED LIGHT",
            [
                "She brought a lamp down so you would not be cold.",
                "The roots came for the light, not for her.",
                "She let them drink. It was the only thing she had left to give.",
            ],
            ["I held the lamp over you all night. The roots were grateful, at least."],
            "The roots drink the light. Take a bloom and the torch comes back."),
        new(
            3,
            "WHAT THE SEA TOOK",
            [
                "The house was beautiful. She built it after the sea took you.",
                "She squared the mortar herself and carried the stone down in her coat.",
                "She never learned to swim either.",
            ],
            ["I built you a house with a roof. You would have liked the roof."],
            "The corridors are the route. The halls only look like rooms."),
        new(
            4,
            "IT HEARS",
            [
                "She sang, because the dark was quiet and she was afraid of it.",
                "The garden learned her voice before it learned her name.",
                "Now it sings back.",
            ],
            ["I sang to keep the quiet off. It has my voice now. Be careful what you say."],
            "It hears you coming, and it is slow. Walk slowly and it stays behind you."),
        new(
            5,
            "THE TRADE",
            [
                "She lit every room so nothing could hide from her.",
                "Not from grief. From you.",
                "She stopped sleeping after that.",
            ],
            ["I lit everything so I would stop seeing you in the corners."],
            "Nothing is hidden here. Do not creep; move before it does."),
        new(
            6,
            "THE BARS",
            [
                "She built a cage for a bird that never came.",
                "She sat in it for a year.",
                "The bars are gilded because you liked the colour.",
            ],
            ["I made it for you. I sat in it instead. It was warm."],
            "No blink down here. The bars are gilded and they are closed."),
        new(
            7,
            "ABSOLUTE",
            [
                "She put the lamp out so she would stop hearing you.",
                "It did not work.",
                "The dark is where she keeps you now.",
            ],
            ["I put the lamp out. You were louder in the dark."],
            "The dark is absolute. Count the turns and trust the wall."),
        new(
            8,
            "TOO MANY DOORS",
            [
                "She built a door for every way you might come back.",
                "Forty doors. None of them opened.",
                "She left them all unlocked.",
            ],
            ["I built them all for you. I could not decide which one to wait at."],
            "The openings are the maze, not the rooms. Take the one that goes down."),
        new(
            9,
            "SMALL",
            [
                "The first garden was the size of your coat.",
                "She could hold it in her arms.",
                "She planted it in the shape of the room you slept in.",
            ],
            ["I wanted one floor I could hold. Just one."],
            "Small floor. Learn it in one pass, because it will not grow."),
        new(
            10,
            "THE SECOND",
            [
                "She grew a second of you out of the roots.",
                "It has your hands. It has her voice.",
                "It does not know your name.",
            ],
            ["I made you again. I am sorry. It was the only thing I knew how to grow."],
            "Two of them walk the branches. Watch both and keep to the middle."),
        new(
            11,
            "PUT IT OUT",
            [
                "The second one followed her up the stairs and would not stop.",
                "She stopped lighting the halls after that.",
                "It finds the light faster than it finds her.",
            ],
            ["It follows the light. I stopped carrying one. You should too."],
            "The glow is gone from them. You hear them late, so listen."),
        new(
            12,
            "WHAT IT MADE",
            [
                "The foundry made the bars of the cage, then nothing for years.",
                "When the garden came it made only one thing.",
                "It made her stop.",
            ],
            ["The foundry could make anything. It could not make you breathe."],
            "Shoves are cheap here. Push one aside and take the corridor."),
        new(
            13,
            "MOTHS",
            [
                "The moths came for her lanterns and would not leave.",
                "She let them stay. Something wanted her light.",
                "She stopped saying they were you.",
            ],
            ["Something wanted my light. For a while I let myself believe it was you."],
            "Lanterns are cheap here. Throw light ahead of you, do not carry it."),
        new(
            14,
            "WHAT IT KEEPS",
            [
                "The shelves are full of people who came down looking for her.",
                "She put them where they would keep.",
                "One is still asleep. She could not finish it.",
            ],
            ["They kept coming down. I could not send them back up. I am sorry."],
            "One of them is asleep. Take your blooms last, so it wakes with nothing to guard."),
        new(
            15,
            "DISTANCE",
            [
                "The water comes in slow and goes out slower.",
                "It takes her footprints before she makes them.",
                "She is not sure she is still here.",
            ],
            ["The water takes my footprints first now. I am not certain I am walking."],
            "Nothing here is in a hurry, and neither is what follows you."),
        new(
            16,
            "NO CORNERS",
            [
                "She braided every dead end so nothing could corner you again.",
                "It worked. Now nothing can hide either.",
                "She did it after the second one learned to wait.",
            ],
            ["No corners. I will not have you cornered again. Not even by me."],
            "The bramble braided every dead end. There is no corner left to hide in."),
        new(
            17,
            "HEAVY DOORS",
            [
                "The vault kept seeds for the spring after the funeral.",
                "The seeds kept. The people did not.",
                "She is the last thing in the house that is not a seed.",
            ],
            ["Seeds keep. People do not. I planted the seeds anyway."],
            "Heavy doors all the way down, and not one of them locked."),
        new(
            18,
            "THREADS",
            [
                "She wove the routes home so you could find it in the dark.",
                "Every thread leads down.",
                "She could not bring herself to weave one up.",
            ],
            ["I wove you a way home. I only knew the way down."],
            "The threads carry you. Step onto one and you are somewhere else."),
        new(
            19,
            "WHAT GREW AFTER",
            [
                "After the fire only the garden grew.",
                "It grows on what she feeds it.",
                "She has very little left to feed it.",
            ],
            ["After the fire, this was the only thing that grew. I fed it what I had."],
            "The light burns down here. Every bloom turns the wick back up."),
        new(
            20,
            "THE ROOT",
            [
                "The root is warm and it is full of her.",
                "Every bloom you picked is in it, and every one she ever planted.",
                "She has been waiting a long time for somebody to kneel.",
            ],
            ["If you are kneeling at the root, you already know what I chose. I am sorry, little one."],
            "The arch holds five breaths. Do not start it with one of them behind you."),
    ];

    // One page object per floor, built once. The catalog is a book: a reader that asks for page 7
    // twice gets the same page, not an equal copy of it.
    private static readonly Dictionary<int, StoryPage> PageByFloor = BuildPageIndex();

    /// <summary>
    /// Gets every page, in run order.
    /// </summary>
    public static IReadOnlyList<StoryPage> Pages { get; } =
        new ReadOnlyCollection<StoryPage>([.. PageByFloor.Values.OrderBy(page => page.FloorNumber)]);

    /// <summary>
    /// Returns the page for <paramref name="floorNumber"/>.
    /// </summary>
    /// <param name="floorNumber">One-based floor number.</param>
    /// <returns>The floor's page.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the shipped campaign has no such floor.
    /// </exception>
    public static StoryPage Page(int floorNumber) =>
        PageByFloor.TryGetValue(floorNumber, out var page)
            ? page
            : throw new ArgumentOutOfRangeException(
                nameof(floorNumber),
                floorNumber,
                $"The gardener's book has pages for floors 1 to {FloorCatalog.ShippedFloorCount}.");

    /// <summary>
    /// Returns the three lines the Echo speaks on <paramref name="floorNumber"/>: two of the
    /// gardener's voice, then the line that teaches the floor's rule.
    /// </summary>
    /// <param name="floorNumber">One-based floor number.</param>
    /// <returns>The lines, in the order they are read.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the shipped campaign has no such floor.
    /// </exception>
    public static IReadOnlyList<string> Echo(int floorNumber)
    {
        var entry = EntryFor(floorNumber);
        return [.. entry.Voice, entry.Hint];
    }

    /// <summary>
    /// Returns the last line the Echo speaks: the floor's rule in the gardener's voice.
    /// </summary>
    /// <param name="floorNumber">One-based floor number.</param>
    /// <returns>The hint.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the shipped campaign has no such floor.
    /// </exception>
    public static string Hint(int floorNumber) => EntryFor(floorNumber).Hint;

    /// <summary>
    /// Returns the extra line the gardener adds when the run is in <paramref name="mood"/>.
    /// </summary>
    /// <param name="mood">How the run looked when the page was turned.</param>
    /// <returns>The line, or <see langword="null"/> when there is nothing to add.</returns>
    /// <remarks>
    /// The line is the same on every floor on purpose. It is the gardener noticing
    /// <i>you</i>, not narrating the place, so it has to hold wherever it lands.
    /// </remarks>
    public static string? MoodLine(StoryMood mood) => mood switch
    {
        StoryMood.LastHeart => "You are down to your last light. She is still waiting at the root.",
        StoryMood.Conditioned => "You gave the garden a rule to keep. It is keeping it.",
        _ => null,
    };

    private static Entry EntryFor(int floorNumber)
    {
        foreach (var entry in Entries)
        {
            if (entry.FloorNumber == floorNumber)
            {
                return entry;
            }
        }

        throw new ArgumentOutOfRangeException(
            nameof(floorNumber),
            floorNumber,
            $"The gardener's book has pages for floors 1 to {FloorCatalog.ShippedFloorCount}.");
    }

    private static Dictionary<int, StoryPage> BuildPageIndex()
    {
        var index = new Dictionary<int, StoryPage>(Entries.Length);

        foreach (var entry in Entries)
        {
            index[entry.FloorNumber] = entry.ToPage();
        }

        return index;
    }

    /// <summary>One floor's authored content, before it is handed out as a page.</summary>
    /// <param name="FloorNumber">The floor the content belongs to.</param>
    /// <param name="Title">The page's own title, drawn above its lines.</param>
    /// <param name="Page">The chapter's lines, three at most.</param>
    /// <param name="Voice">What the Echo says before its hint.</param>
    /// <param name="Hint">The Echo's last line: the floor's rule in plain words.</param>
    private sealed record Entry(
        int FloorNumber,
        string Title,
        string[] Page,
        string[] Voice,
        string Hint)
    {
        public StoryPage ToPage() => new(FloorNumber, Title, Array.AsReadOnly(Page));
    }
}
