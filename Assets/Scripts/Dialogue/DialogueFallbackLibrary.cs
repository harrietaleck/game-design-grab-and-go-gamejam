using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Prewritten AJ radio lines (Task 4.5 / 3.3). Instant, offline, and used whenever the LLM is
/// unavailable, slow, over budget, or the event is urgent. Avoids repeating a line until the pool is used up.
/// </summary>
public static class DialogueFallbackLibrary
{
    private static readonly Dictionary<GameEventType, string[]> Lines = new()
    {
        [GameEventType.RunStarted] = new[]
        {
            "Claire, you hearing me? Bag's heavy, legs are fast. Keep up!",
            "Okay, okay, we're moving. Grab whatever falls out, yeah?",
            "Star of the show is on the move. You're... the helpful co-star.",
            "Two minutes, Claire. Two minutes and we're legends.",
        },
        [GameEventType.CoinCollected] = new[]
        {
            "Every coin counts. Well, most of them.",
            "Nice pickup. I meant to drop that. Totally on purpose.",
        },
        [GameEventType.GoldStackCollected] = new[]
        {
            "Tell me you got that gold stack. Please tell me you got it.",
            "THAT'S the good stuff! Okay, you're officially useful.",
            "Gold! See, this is why I keep you around.",
            "Careful with that one, it's worth more than my rent.",
        },
        [GameEventType.MultipleLootCollected] = new[]
        {
            "You're vacuuming that road, Claire. Respect.",
            "Look at you go. Am I the sidekick now?",
            "Okay, show-off. Leave some for the pigeons.",
        },
        [GameEventType.ClaireCollision] = new[]
        {
            "Claire! Cars hurt! Eyes up!",
            "You okay back there? That sounded expensive.",
            "Dodge the cars, grab the coins. In that order!",
            "Ouch. I felt that from here.",
        },
        [GameEventType.ClaireLowHealth] = new[]
        {
            "Claire, you're limping. Don't you dare quit on me now.",
            "Hey. HEY. Slow is fine, stopping is not.",
            "You look rough. Stay with me, we're nearly out.",
        },
        [GameEventType.AJNearMiss] = new[]
        {
            "Whoa! That bumper kissed my shoe!",
            "Close one. I meant that. Style points.",
            "Nobody saw that. Nobody saw that, right?",
        },
        [GameEventType.PolicePressureHigh] = new[]
        {
            "Sirens are getting louder, Claire. Do the thing!",
            "They're onto us. Maybe hit that distraction?",
            "Heat's rising! Less crashing, more vanishing!",
        },
        [GameEventType.DistractionUsed] = new[]
        {
            "Ha! They're looking the wrong way. Genius. My genius, obviously.",
            "Smooth. Remind me to take credit for that later.",
            "Nice distraction. Breathing room!",
        },
        [GameEventType.AJCaught] = new[]
        {
            "Claire! They've got me! Get to my lane, NOW!",
            "Uh, small problem. I'm stuck. Come get me!",
            "Okay, I may need saving. Just this once!",
        },
        [GameEventType.AJRescued] = new[]
        {
            "You came back for me! I mean... obviously you did. Go go go!",
            "Okay, that was heroic. Don't let it go to your head.",
            "Saved by the co-star. Don't tell anyone.",
            "I owe you one. Maybe half of one. Run!",
        },
        [GameEventType.TimeUp] = new[]
        {
            "That's time! Did we make it?",
        },
        [GameEventType.RunWon] = new[]
        {
            "We did it! Well, I did it. You helped. A lot. Fine, we did it.",
            "Clean getaway! Drinks are on me. With your share.",
            "Legendary. Same time next week, partner?",
        },
        [GameEventType.RunFailed] = new[]
        {
            "That... did not go to plan. Next time, Claire.",
            "Okay, regroup. We'll get it right next run.",
            "Busted. Remind me to make you the main character next time.",
        },
    };

    private static readonly Dictionary<GameEventType, List<int>> Unused = new();

    public static bool HasLines(GameEventType type) => Lines.ContainsKey(type);

    public static string Pick(GameEventType type)
    {
        if (!Lines.TryGetValue(type, out var pool) || pool.Length == 0)
            return null;
        if (!Unused.TryGetValue(type, out var unused) || unused.Count == 0)
        {
            unused = new List<int>(pool.Length);
            for (int i = 0; i < pool.Length; i++) unused.Add(i);
            Unused[type] = unused;
        }
        int pick = Random.Range(0, unused.Count);
        int index = unused[pick];
        unused.RemoveAt(pick);
        return pool[index];
    }

    public static void ResetRepeats() => Unused.Clear();
}
