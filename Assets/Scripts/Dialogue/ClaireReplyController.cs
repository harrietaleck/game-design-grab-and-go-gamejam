using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Claire's optional radio replies (Task 4.10). After some AJ lines, three short replies appear for a few seconds.
/// Keys 1/2/3 pick one; ignoring them is fine. Replies never pause gameplay, never appear during danger,
/// and only affect dialogue (they are stored in the per-run memory and sent with the next AJ request).
/// </summary>
public class ClaireReplyController : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        GameStartFlow.RegisterBootstrap(flow =>
        {
            if (flow.GetComponent<ClaireReplyController>() == null)
                flow.gameObject.AddComponent<ClaireReplyController>();
        });
    }

    public float optionsVisibleSeconds = 4f;
    public float minSecondsBetweenPrompts = 18f;
    public int maxPromptsPerRun = 4;

    private enum Tone { Supportive, Focused, Sassy }

    private struct Reply
    {
        public string text;
        public Tone tone;
        public Reply(string t, Tone tone) { text = t; this.tone = tone; }
    }

    private static readonly Dictionary<GameEventType, Reply[]> Options = new()
    {
        [GameEventType.RunStarted] = new[]
        {
            new Reply("Right behind you. Don't trip.", Tone.Supportive),
            new Reply("Less talking, more running.", Tone.Focused),
            new Reply("Co-star? I'm the director.", Tone.Sassy),
        },
        [GameEventType.GoldStackCollected] = new[]
        {
            new Reply("Got it. Keep 'em coming.", Tone.Supportive),
            new Reply("Focus. The police are closing in.", Tone.Focused),
            new Reply("Remind me who's doing all the work?", Tone.Sassy),
        },
        [GameEventType.MultipleLootCollected] = new[]
        {
            new Reply("Teamwork, partner.", Tone.Supportive),
            new Reply("Just keep your eyes on the road.", Tone.Focused),
            new Reply("I'm carrying this whole operation.", Tone.Sassy),
        },
        [GameEventType.AJRescued] = new[]
        {
            new Reply("Always. Now move!", Tone.Supportive),
            new Reply("Don't get caught again.", Tone.Focused),
            new Reply("You owe me a whole one.", Tone.Sassy),
        },
        [GameEventType.DistractionUsed] = new[]
        {
            new Reply("Told you I had a plan.", Tone.Supportive),
            new Reply("That buys us seconds. Use them.", Tone.Focused),
            new Reply("Your genius? Cute.", Tone.Sassy),
        },
    };

    private AJDialogueController _dialogue;
    private GameStartFlow _flow;
    private Reply[] _current;
    private float _hideAt;
    private float _lastPromptAt = -100f;
    private int _promptsThisRun;

    private void Start()
    {
        _flow = GetComponent<GameStartFlow>();
        _dialogue = GetComponent<AJDialogueController>();
        if (_dialogue != null) _dialogue.LineSpoken += OnLineSpoken;
    }

    private void OnDestroy()
    {
        if (_dialogue != null) _dialogue.LineSpoken -= OnLineSpoken;
    }

    private void OnEnable() => GameEvents.OnEvent += OnGameEvent;
    private void OnDisable() => GameEvents.OnEvent -= OnGameEvent;

    private void OnGameEvent(GameEventData e)
    {
        if (e.type == GameEventType.RunStarted) { _promptsThisRun = 0; _lastPromptAt = -100f; Clear(); }
        // Danger: withdraw options immediately.
        if (e.type == GameEventType.AJCaught || e.type == GameEventType.PolicePressureHigh ||
            e.type == GameEventType.RunWon || e.type == GameEventType.RunFailed)
            Clear();
    }

    private void OnLineSpoken(GameEventType type, string line)
    {
        if (_flow == null || _flow.State != GameStartFlow.RunState.Playing) return;
        if (!Options.TryGetValue(type, out var replies)) return;
        if (_promptsThisRun >= maxPromptsPerRun || Time.unscaledTime - _lastPromptAt < minSecondsBetweenPrompts) return;
        if (PolicePressure.Instance != null && PolicePressure.Instance.IsHigh) return; // never during danger

        _current = replies;
        _hideAt = Time.unscaledTime + optionsVisibleSeconds;
        _lastPromptAt = Time.unscaledTime;
        _promptsThisRun++;
        _dialogue.UI.ShowOptions($"[1] {replies[0].text}   [2] {replies[1].text}   [3] {replies[2].text}");
    }

    private void Update()
    {
        if (_current == null) return;
        if (_flow == null || _flow.State != GameStartFlow.RunState.Playing || Time.unscaledTime >= _hideAt)
        {
            Clear();
            return;
        }
        int pick = -1;
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) pick = 0;
        else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) pick = 1;
        else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) pick = 2;
        if (pick < 0) return;

        var reply = _current[pick];
        _current = null;
        _dialogue.UI.ShowClaireReply(reply.text);
        _dialogue.Remember("Claire", reply.text);
        _dialogue.LastClaireReply = reply.text;
        // Simple relationship variables for the next request (dialogue only).
        switch (reply.tone)
        {
            case Tone.Supportive: _dialogue.Trust = Mathf.Clamp(_dialogue.Trust + 1, -10, 10); break;
            case Tone.Sassy: _dialogue.Tension = Mathf.Clamp(_dialogue.Tension + 1, -10, 10); break;
            case Tone.Focused: _dialogue.Tension = Mathf.Clamp(_dialogue.Tension - 1, -10, 10); break;
        }
    }

    private void Clear()
    {
        if (_current != null && _dialogue != null && _dialogue.UI != null) _dialogue.UI.ShowOptions("");
        _current = null;
    }
}
