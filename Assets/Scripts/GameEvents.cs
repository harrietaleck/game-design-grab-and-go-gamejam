using System;
using UnityEngine;

/// <summary>Why a run ended. Exactly one is reported per run.</summary>
public enum RunEndReason
{
    None,
    TimeUpEscaped,      // Timer reached 0 with Claire alive and AJ free -> success
    ClaireOutOfHealth,  // Claire's health reached 0
    AJCaptured,         // Rescue failed (Claire missed AJ)
    TimeUpWhileCaught,  // Timer reached 0 while AJ was still caught
    Abandoned           // Player quit to menu mid-run
}

/// <summary>Gameplay facts other systems (HUD, dialogue, audio) can react to. Never fabricated: raised only by the system that owns the fact.</summary>
public enum GameEventType
{
    RunStarted,
    CoinCollected,
    GoldStackCollected,
    MultipleLootCollected,
    ClaireCollision,
    ClaireLowHealth,
    AJNearMiss,
    PolicePressureHigh,
    DistractionUsed,
    AJCaught,
    AJRescued,
    RunWon,
    RunFailed,
    TimeUp
}

/// <summary>Snapshot of authoritative run values attached to every event.</summary>
public struct GameEventData
{
    public GameEventType type;
    public int runId;
    public float timeRemaining;
    public float claireHealth;
    public int lootValue;
    public float policePressure;
    public int value;          // event-specific (e.g. loot value of the pickup)
    public string detail;      // event-specific short text (e.g. capture cause)
    public float raisedAtTime; // Time.time when raised
}

/// <summary>
/// Lightweight static event hub. Subscribers must unsubscribe in OnDisable.
/// Only GameStartFlow / the owning system should call Raise.
/// </summary>
public static class GameEvents
{
    public static event Action<GameEventData> OnEvent;

    public static void Raise(GameEventType type, int value = 0, string detail = null)
    {
        var flow = GameStartFlow.Instance;
        var data = new GameEventData
        {
            type = type,
            runId = GameStartFlow.CurrentRunId,
            timeRemaining = flow != null ? flow.TimeRemaining : 0f,
            claireHealth = flow != null ? flow.ClaireHealth : 0f,
            lootValue = flow != null ? flow.LootValue : 0,
            policePressure = flow != null ? flow.PolicePressureValue : 0f,
            value = value,
            detail = detail,
            raisedAtTime = Time.time
        };

        try
        {
            OnEvent?.Invoke(data);
        }
        catch (Exception e)
        {
            // A broken listener must never break gameplay.
            Debug.LogException(e);
        }
    }
}
