using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Compact, verified game context sent with a dialogue request (Task 4.7). Built only from Unity's authoritative state.</summary>
[Serializable]
public class DialogueRequest
{
    public string @event;
    public string gameState;
    public int timeRemaining;
    public int claireHealth;
    public int lootValue;
    public int coins;
    public int goldStacks;
    public string policePressure;   // Low / Medium / High
    public string detail;           // e.g. capture cause
    public List<DialogueTurn> recentDialogue = new();
    public string claireReply;
    public string persona;
    public int trust;
    public int tension;

    [NonSerialized] public int runId;
    [NonSerialized] public int requestId;
    [NonSerialized] public float createdAt;
}

[Serializable]
public class DialogueTurn
{
    public string speaker;
    public string text;
}

/// <summary>
/// Decides when AJ speaks (Task 4.4): priorities, cooldowns, run-state checks, one line at a time,
/// LLM budget, stale-response rejection, and instant prewritten fallbacks. Never changes gameplay.
/// </summary>
public class AJDialogueController : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        GameStartFlow.RegisterBootstrap(flow =>
        {
            if (flow.GetComponent<AJDialogueController>() == null)
                flow.gameObject.AddComponent<AJDialogueController>();
        });
    }

    public enum Priority { Low = 0, Normal = 1, High = 2, Critical = 3 }

    /// <summary>
    /// Remote line provider (set by DialogueAPIClient when a backend is configured).
    /// Call: provider(request, onDone) -> true if the request was sent. onDone(null) on any failure.
    /// </summary>
    public static Func<DialogueRequest, Action<string>, bool> RemoteProvider;

    public static AJDialogueController Instance { get; private set; }

    [Header("Character")]
    public AJPersonalityProfile personality;

    [Header("Policy")]
    [Tooltip("Minimum seconds between any two AJ lines (Critical events ignore this).")]
    public float globalMinGapSeconds = 3.5f;
    [Tooltip("Minimum seconds before the same event can trigger AJ again.")]
    public float perEventCooldownSeconds = 10f;
    [Range(0f, 1f)] public float coinLineChance = 0.06f;
    [Tooltip("Collisions only get a reaction if health is at or below this fraction, or two hits happen close together.")]
    [Range(0f, 1f)] public float collisionReactHealthFraction = 0.7f;

    [Header("LLM Budget")]
    [Tooltip("Maximum LLM-generated lines per run (tuning parameter). 0 = local lines only.")]
    public int maxRemoteLinesPerRun = 5;
    [Tooltip("Seconds to wait for the backend before using a prewritten line.")]
    public float remoteTimeoutSeconds = 2.5f;
    [Tooltip("A response older than this (seconds since the event) is discarded as stale.")]
    public float maxLineAgeSeconds = 5f;
    public bool verboseLogging = false;

    public RadioDialogueUI UI { get; private set; }
    public int RemoteLinesUsed { get; private set; }
    public int RemoteFailures { get; private set; }

    /// <summary>Raised after AJ speaks a line (for Claire reply options).</summary>
    public event Action<GameEventType, string> LineSpoken;

    // Lightweight per-run memory (Task 4.11)
    public readonly List<DialogueTurn> RecentTurns = new();
    public string LastClaireReply { get; set; }
    public int Trust { get; set; }
    public int Tension { get; set; }
    private const int MaxTurns = 6;

    private GameStartFlow _flow;
    private int _runId = -1;
    private float _lastSpokeAt = -100f;
    private Priority _showingPriority;
    private readonly Dictionary<GameEventType, float> _lastByEvent = new();
    private int _requestCounter;
    private int _pendingRequestId = -1;
    private float _lastCollisionAt = -100f;

    private void Awake()
    {
        Instance = this;
        _flow = GetComponent<GameStartFlow>();
        if (personality == null) personality = AJPersonalityProfile.Default;
        UI = gameObject.AddComponent<RadioDialogueUI>();
        UI.Build(transform);
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }
    private void OnEnable() => GameEvents.OnEvent += OnGameEvent;
    private void OnDisable() => GameEvents.OnEvent -= OnGameEvent;

    public static string EventName(GameEventType t)
    {
        switch (t)
        {
            case GameEventType.RunStarted: return "RUN_STARTED";
            case GameEventType.CoinCollected: return "COIN_COLLECTED";
            case GameEventType.GoldStackCollected: return "GOLD_BAR_COLLECTED"; // design name; in-game it is a gold coin stack
            case GameEventType.MultipleLootCollected: return "MULTIPLE_LOOT_COLLECTED";
            case GameEventType.ClaireCollision: return "CLAIRE_COLLISION";
            case GameEventType.ClaireLowHealth: return "CLAIRE_LOW_HEALTH";
            case GameEventType.AJNearMiss: return "AJ_NEAR_MISS";
            case GameEventType.PolicePressureHigh: return "POLICE_PRESSURE_HIGH";
            case GameEventType.DistractionUsed: return "DISTRACTION_USED";
            case GameEventType.AJCaught: return "AJ_CAUGHT";
            case GameEventType.AJRescued: return "AJ_RESCUED";
            case GameEventType.RunWon: return "RUN_WON";
            case GameEventType.RunFailed: return "RUN_FAILED";
            case GameEventType.TimeUp: return "TIME_UP";
            default: return t.ToString();
        }
    }

    private static Priority PriorityOf(GameEventType t)
    {
        switch (t)
        {
            case GameEventType.CoinCollected:
            case GameEventType.MultipleLootCollected:
                return Priority.Low;
            case GameEventType.RunStarted:
            case GameEventType.GoldStackCollected:
            case GameEventType.AJNearMiss:
            case GameEventType.DistractionUsed:
                return Priority.Normal;
            case GameEventType.ClaireCollision:
            case GameEventType.ClaireLowHealth:
            case GameEventType.PolicePressureHigh:
                return Priority.High;
            default:
                return Priority.Critical;
        }
    }

    /// <summary>Urgent warnings always use prewritten lines (instant).</summary>
    private static bool IsUrgent(GameEventType t) =>
        t == GameEventType.ClaireCollision || t == GameEventType.AJCaught || t == GameEventType.AJNearMiss ||
        t == GameEventType.TimeUp || t == GameEventType.PolicePressureHigh;

    /// <summary>Events worth a contextual (LLM) line when budget allows.</summary>
    private static bool IsRemoteCandidate(GameEventType t) =>
        t == GameEventType.GoldStackCollected || t == GameEventType.MultipleLootCollected ||
        t == GameEventType.AJRescued || t == GameEventType.ClaireLowHealth ||
        t == GameEventType.RunWon || t == GameEventType.RunFailed || t == GameEventType.DistractionUsed;

    private void OnGameEvent(GameEventData e)
    {
        if (_flow == null) return;

        if (e.type == GameEventType.RunStarted)
            ResetForRun(e.runId);
        if (e.runId != _runId) return;

        var state = _flow.State;
        if (state == GameStartFlow.RunState.MainMenu) return;
        // After the run ends only result commentary is allowed.
        if (state == GameStartFlow.RunState.Results && e.type != GameEventType.RunWon && e.type != GameEventType.RunFailed) return;
        // Skip the generic TIME_UP line; the RUN_WON / RUN_FAILED line follows immediately.
        if (e.type == GameEventType.TimeUp) return;

        var priority = PriorityOf(e.type);

        // During the rescue only rescue-relevant lines play.
        if (state == GameStartFlow.RunState.AJCaught && priority < Priority.Critical && e.type != GameEventType.ClaireLowHealth) return;
        // Suppress banter when danger is high.
        if (priority == Priority.Low && PolicePressure.Instance != null && PolicePressure.Instance.IsHigh) return;

        if (e.type == GameEventType.CoinCollected && UnityEngine.Random.value > coinLineChance) return;
        if (e.type == GameEventType.ClaireCollision)
        {
            bool closeTogether = Time.time - _lastCollisionAt < 6f;
            _lastCollisionAt = Time.time;
            bool lowish = e.claireHealth <= _flow.MaxHealth * collisionReactHealthFraction;
            if (!closeTogether && !lowish) return;
        }

        float now = Time.unscaledTime;
        if (_lastByEvent.TryGetValue(e.type, out float last) && now - last < (priority == Priority.Critical ? 2f : perEventCooldownSeconds)) return;
        if (priority < Priority.Critical && now - _lastSpokeAt < globalMinGapSeconds) return;
        // One line at a time: only High+ may replace a line that is still on screen.
        if (UI.IsShowing && priority < Priority.High && priority <= _showingPriority) return;

        _lastByEvent[e.type] = now;
        Respond(e, priority);
    }

    private void Respond(GameEventData e, Priority priority)
    {
        bool remoteAllowed = RemoteProvider != null && RemoteLinesUsed < maxRemoteLinesPerRun &&
                             !IsUrgent(e.type) && IsRemoteCandidate(e.type);
        if (!remoteAllowed)
        {
            Speak(e.type, DialogueFallbackLibrary.Pick(e.type), priority);
            return;
        }

        var request = BuildRequest(e);
        int id = request.requestId;
        _pendingRequestId = id;
        bool sent = false;
        try
        {
            sent = RemoteProvider(request, line => OnRemoteLine(request, e.type, priority, line));
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }

        if (!sent)
        {
            _pendingRequestId = -1;
            Speak(e.type, DialogueFallbackLibrary.Pick(e.type), priority);
            return;
        }
        RemoteLinesUsed++;
        StartCoroutine(RemoteTimeout(id, e.type, priority));
    }

    private IEnumerator RemoteTimeout(int requestId, GameEventType type, Priority priority)
    {
        yield return new WaitForSecondsRealtime(remoteTimeoutSeconds);
        if (_pendingRequestId != requestId) yield break; // already answered or superseded
        _pendingRequestId = -1;
        RemoteFailures++;
        if (verboseLogging) Debug.Log($"[AJDialogue] Remote line timed out for {EventName(type)}; using fallback.");
        if (StillRelevant(type)) Speak(type, DialogueFallbackLibrary.Pick(type), priority);
    }

    private void OnRemoteLine(DialogueRequest request, GameEventType type, Priority priority, string line)
    {
        // Stale checks: superseded, different run, too old, or no longer relevant.
        if (request.requestId != _pendingRequestId || request.runId != _runId)
        {
            if (verboseLogging) Debug.Log($"[AJDialogue] Discarded stale response #{request.requestId}.");
            return;
        }
        _pendingRequestId = -1;
        if (Time.unscaledTime - request.createdAt > maxLineAgeSeconds || !StillRelevant(type))
        {
            if (verboseLogging) Debug.Log($"[AJDialogue] Discarded late/irrelevant response for {EventName(type)}.");
            return;
        }

        string clean = DialogueText.Sanitize(line, personality != null ? personality.maxLineCharacters : 120);
        if (string.IsNullOrEmpty(clean))
        {
            RemoteFailures++;
            clean = DialogueFallbackLibrary.Pick(type);
        }
        Speak(type, clean, priority);
    }

    private bool StillRelevant(GameEventType type)
    {
        if (_flow == null) return false;
        var s = _flow.State;
        if (type == GameEventType.RunWon || type == GameEventType.RunFailed)
            return s == GameStartFlow.RunState.Results;
        if (s == GameStartFlow.RunState.Results || s == GameStartFlow.RunState.MainMenu) return false;
        if (type == GameEventType.AJRescued) return s == GameStartFlow.RunState.Playing;
        return true;
    }

    private void Speak(GameEventType type, string line, Priority priority)
    {
        if (string.IsNullOrEmpty(line) || UI == null) return;
        UI.DockTop(_flow != null && _flow.State == GameStartFlow.RunState.Results);
        UI.Show(personality != null ? personality.characterName : "AJ", line);
        _showingPriority = priority;
        _lastSpokeAt = Time.unscaledTime;
        Remember("AJ", line);
        if (verboseLogging) Debug.Log($"[AJDialogue] {EventName(type)}: {line}");
        LineSpoken?.Invoke(type, line);
    }

    public void Remember(string speaker, string text)
    {
        RecentTurns.Add(new DialogueTurn { speaker = speaker, text = text });
        while (RecentTurns.Count > MaxTurns) RecentTurns.RemoveAt(0);
    }

    private DialogueRequest BuildRequest(GameEventData e)
    {
        var f = _flow;
        float p = PolicePressure.Instance != null ? PolicePressure.Instance.Normalized : 0f;
        var req = new DialogueRequest
        {
            @event = EventName(e.type),
            gameState = f.State.ToString(),
            timeRemaining = Mathf.CeilToInt(f.TimeRemaining),
            claireHealth = Mathf.CeilToInt(f.ClaireHealth),
            lootValue = f.LootValue,
            coins = f.CoinCount,
            goldStacks = f.GoldStackCount,
            policePressure = p >= 0.7f ? "High" : p >= 0.4f ? "Medium" : "Low",
            detail = e.detail,
            claireReply = LastClaireReply,
            persona = personality != null ? personality.PersonaForRequest() : null,
            trust = Trust,
            tension = Tension,
            runId = _runId,
            requestId = ++_requestCounter,
            createdAt = Time.unscaledTime
        };
        // Only the last few turns are sent.
        int start = Mathf.Max(0, RecentTurns.Count - 4);
        for (int i = start; i < RecentTurns.Count; i++) req.recentDialogue.Add(RecentTurns[i]);
        LastClaireReply = null; // a reply is used once
        return req;
    }

    private void ResetForRun(int runId)
    {
        _runId = runId;
        _pendingRequestId = -1; // any in-flight response from a previous run is now stale
        _lastByEvent.Clear();
        _lastSpokeAt = -100f;
        RemoteLinesUsed = 0;
        RemoteFailures = 0;
        RecentTurns.Clear();
        LastClaireReply = null;
        Trust = 0;
        Tension = 0;
        DialogueFallbackLibrary.ResetRepeats();
        StopAllCoroutines();
    }
}

/// <summary>Validation for untrusted model output: plain single line, bounded length, no markup.</summary>
public static class DialogueText
{
    public static string Sanitize(string raw, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var sb = new System.Text.StringBuilder(raw.Length);
        foreach (char c in raw)
        {
            if (c == '\n' || c == '\r' || c == '\t') { sb.Append(' '); continue; }
            if (char.IsControl(c)) continue;
            if (c == '<' || c == '>' || c == '{' || c == '}' || c == '`') continue; // no rich text / code
            sb.Append(c);
        }
        string s = System.Text.RegularExpressions.Regex.Replace(sb.ToString(), @"\s+", " ").Trim().Trim('"', '\'', ' ');
        if (s.StartsWith("AJ:", StringComparison.OrdinalIgnoreCase)) s = s.Substring(3).Trim();
        if (s.Length == 0 || s.Length > maxChars) return null;
        return s;
    }
}
