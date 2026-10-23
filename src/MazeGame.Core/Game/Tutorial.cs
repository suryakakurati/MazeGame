using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MazeGame.Core.Game;

/// <summary>
/// The guided tutorial's pages, in the order the title screen shows them.
/// </summary>
/// <remarks>
/// <para>
/// This is the "Tutorial" entry on the main menu: a page-by-page walkthrough of the descent and of
/// every ability the player carries. It is deliberately exhaustive rather than discoverable — a
/// player who reads all of it has been told every control, every hazard and every price before
/// their first floor.
/// </para>
/// <para>
/// The pages are content, not rules: nothing here changes a game. Both frontends draw the same
/// list, so the terminal and the window cannot drift into teaching different keys.
/// </para>
/// </remarks>
public static class Tutorial
{
    private static readonly TutorialStep[] Pages =
    [
        new()
        {
            Caption = "WELCOME",
            Title = "A DESCENT IN THE DARK",
            Lines =
            [
                "You are going down twenty floors of a bioluminescent cave.",
                "Every floor hides blooms and one locked arch. Pick every bloom, open the arch, and go deeper.",
                "This guide walks through every screen, every danger and every ability. Press ENTER to turn each page.",
            ],
        },
        new()
        {
            Caption = "STEP 1 - WALKING",
            Title = "ONE PRESS, ONE TILE",
            Lines =
            [
                "The explorer always stands on one tile. A tap of a direction moves exactly one tile; hold it and you keep walking at the floor's pace.",
                "You begin faster than the thing that hunts you. Sprint (hold SHIFT) buys a short burst, but the stamina bar must refill before it can be used again.",
                "Walls block you. The dark hides everything beyond your torch.",
            ],
            Keys = "W A S D / ARROWS: WALK      SHIFT: SPRINT",
        },
        new()
        {
            Caption = "STEP 2 - BLOOMS",
            Title = "PICK EVERY BLOOM",
            Lines =
            [
                "A bloom is the floor's key: a small glowing flower on the ground. Walk onto it to pick it up.",
                "The footer counts what is left. The arch at the floor's end stays locked until the count reaches zero.",
                "Losing a heart never takes a bloom away. Progress is only ever forward.",
            ],
            Keys = "WALK ONTO A BLOOM TO PICK IT UP",
        },
        new()
        {
            Caption = "STEP 3 - THE ARCH",
            Title = "OPEN THE ARCH",
            Lines =
            [
                "Once every bloom is picked, the arch opens. Stand on it to clear the floor.",
                "Clearing a floor turns a page of the gardener's book and carries your hearts into the next floor.",
                "The twentieth arch wins the run. The game only ends early if your last heart is spent.",
            ],
            Keys = "STAND ON THE OPEN ARCH",
        },
        new()
        {
            Caption = "STEP 4 - HUNTERS",
            Title = "DO NOT SHARE A TILE",
            Lines =
            [
                "One hunter stalks floors 1 to 9, two on floors 10 to 15, and three on floors 16 to 20. Each has its own glow and its own route.",
                "A hunter only knows where you are when you are close. Keep out of its light, break line of sight, and keep moving.",
                "If a hunter reaches your tile it catches you: it is thrown far away, every hunter is stunned, and the chase starts again. That costs hearts, never blooms.",
            ],
            Keys = "ITS GLOW MEANS IT IS CLOSE",
        },
        new()
        {
            Caption = "STEP 5 - HEARTS",
            Title = "EVERY CATCH COSTS MORE",
            Lines =
            [
                "You start with three hearts and there is no ceiling: heals add to the total and hearts carry between floors.",
                "A catch costs one heart on floors 1 to 4, then one more every four floors, up to five hearts from floor 17 down.",
                "At the last heart the run is over. The death card tells you which floor you reached.",
            ],
            Keys = "HEARTS: THE ONLY THING YOU CAN RUN OUT OF",
        },
        new()
        {
            Caption = "STEP 6 - BLINK",
            Title = "BLINK THROUGH A WALL",
            Lines =
            [
                "Face a wall and press SPACE to blink through it. The wall is removed and you step into the cell beyond.",
                "Use it to cut a corner when a hunter has the long way round, or to escape a dead end.",
                "Blink charges refill on every new floor.",
            ],
            Keys = "SPACE: BLINK",
        },
        new()
        {
            Caption = "STEP 7 - VEIL AND DRAG",
            Title = "HIDE, THEN SLOW THE HUNT",
            Lines =
            [
                "Press V and stand still for a moment to veil: you are unseen for three seconds, and the hunt searches where it last saw you.",
                "Press T to drag: every hunter's clock slows for a few seconds, then speeds up afterwards as the price.",
                "Veil is for walking out of a room a hunter is searching. Drag is for making distance when it is in sight.",
            ],
            Keys = "V: VEIL      T: DRAG",
        },
        new()
        {
            Caption = "STEP 8 - LANTERN AND SHOVE",
            Title = "THROW LIGHT, PUSH BACK",
            Lines =
            [
                "Press L to throw your lantern: its light moves up to five cells away and burns for eight seconds. The hunters walk toward the light, so it is a decoy as well as a torch.",
                "Press F to shove every hunter next to you two cells away and stun them. It is the one way to make a catch impossible rather than unlikely.",
                "Both are per-floor charges, and a charge is only spent when the ability actually works.",
            ],
            Keys = "L: LANTERN      F: SHOVE",
        },
        new()
        {
            Caption = "STEP 9 - SAFE ROOMS",
            Title = "A SEAM IN THE WALL",
            Lines =
            [
                "Some floors hide a safe room behind a gold seam in the wall. Step inside and press E to open its card.",
                "Nothing outside the room moves while you are inside. Rooms trade in hearts: a stake at the casino, a card at the stall, a rule at the toll.",
                "The Confessional takes a card and a heart. Refusing costs the same, and it is the room that chooses the card. The casino's odds are printed, and the last heart can never be staked.",
            ],
            Keys = "E: OPEN THE ROOM",
        },
        new()
        {
            Caption = "STEP 10 - ALTARS AND RELICS",
            Title = "A STONE THAT REMEMBERS",
            Lines =
            [
                "Some floors hide an altar off the short way to the arch: a plinth with a floating gem. Walk onto it to take the relic above it.",
                "A relic is permanent: it stays for the rest of the run. The rarest are legendary, and RUN DETAILS in the pause menu lists everything you carry.",
                "Touching an altar wakes every sleeping hunter. The relic is free; the walk back is not, and the altar is never on the fastest route to the arch.",
            ],
            Keys = "WALK ONTO THE ALTAR TO TAKE ITS RELIC",
        },
        new()
        {
            Caption = "STEP 11 - THE SHAPE OF A RUN",
            Title = "THE CAVE CHANGES",
            Lines =
            [
                "The first three floors are always the same, so the lesson is the same every time. Below them, each floor bends with the run's seed.",
                "A quiet floor has one hunter fewer and more light. A greedy one adds a bloom and a hunter. A haunted one takes a step of light and opens the walls. A starved one has a bloom fewer and a hungrier hunt.",
                "Every floor's intro card says what kind of descent it is. The same twenty places are a different campaign from one run to the next.",
            ],
            Keys = "READ THE FLOOR'S INTRO CARD",
        },
        new()
        {
            Caption = "STEP 12 - THE DRAFT",
            Title = "EVERY CARD GIVES AND TAKES",
            Lines =
            [
                "Between floors, choose one of three cards. Each card states both halves on its face: what it gives and what it costs.",
                "Press R to reroll the offers with hearts: the first reroll on a floor costs one, the second two, and so on. The price resets on the next floor.",
                "Press S to skip the draft instead and heal one heart. Press 1, 2 or 3 to take a card directly.",
            ],
            Keys = "A / D: CHOOSE      ENTER: TAKE      R: REROLL      S: SKIP",
        },
        new()
        {
            Caption = "STEP 13 - THE PAUSE MENU",
            Title = "STOP, THEN CONTINUE",
            Lines =
            [
                "Press ESC during a floor to pause. The pause menu offers CONTINUE, which puts you straight back where you stood, RUN DETAILS, which lists every card, relic and condition the run carries, CARDS, the full catalog, SAVE & QUIT, which keeps the run for CONTINUE on the title, and ABANDON RUN, which throws the run away.",
                "ESC also closes a safe-room card without spending it, and cancels an aim.",
                "That is everything. Press ENTER to walk the practice floor once, or ESC to go back to the title and descend for real.",
            ],
            Keys = "ESC: PAUSE      ENTER: CONFIRM",
        },
    ];

    /// <summary>Gets the tutorial's pages, in display order, as a read-only list.</summary>
    public static IReadOnlyList<TutorialStep> Steps { get; } =
        new ReadOnlyCollection<TutorialStep>(Pages);

    /// <summary>Gets how many pages the tutorial has.</summary>
    public static int PageCount => Pages.Length;

    /// <summary>
    /// Returns the page at <paramref name="index"/>, clamped into the tutorial.
    /// </summary>
    /// <param name="index">The zero-based page number.</param>
    /// <returns>The page; never null.</returns>
    public static TutorialStep Page(int index) => Pages[Math.Clamp(index, 0, Pages.Length - 1)];
}
