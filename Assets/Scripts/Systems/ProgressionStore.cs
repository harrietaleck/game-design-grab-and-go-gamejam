using UnityEngine;

/// <summary>
/// Modest progression (Task 6.3): personal bests saved locally with PlayerPrefs.
/// Recorded once per finished run (abandoned runs are not counted).
/// </summary>
public class ProgressionStore : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        GameStartFlow.RegisterBootstrap(flow =>
        {
            if (flow.GetComponent<ProgressionStore>() == null)
                flow.gameObject.AddComponent<ProgressionStore>();
        });
        RunOverlayUI.ExtraResultsLines = BuildResultsLines;
    }

    private const string KeyBestLoot = "gg_best_loot";
    private const string KeyBestTime = "gg_best_time";
    private const string KeyGetaways = "gg_getaways";
    private const string KeyRuns = "gg_runs";
    private const string KeyRescues = "gg_rescues";

    public static int BestLoot => PlayerPrefs.GetInt(KeyBestLoot, 0);
    public static float BestTime => PlayerPrefs.GetFloat(KeyBestTime, 0f);
    public static int Getaways => PlayerPrefs.GetInt(KeyGetaways, 0);
    public static int Runs => PlayerPrefs.GetInt(KeyRuns, 0);
    public static int Rescues => PlayerPrefs.GetInt(KeyRescues, 0);

    // Results of the last recorded run (for "NEW BEST" labels).
    public static bool LastRunNewBestLoot { get; private set; }
    public static bool LastRunNewBestTime { get; private set; }

    private GameStartFlow _flow;

    private void Awake()
    {
        _flow = GetComponent<GameStartFlow>();
    }

    private void OnEnable() { if (_flow != null) _flow.RunEnded += OnRunEnded; }
    private void OnDisable() { if (_flow != null) _flow.RunEnded -= OnRunEnded; }

    private void OnRunEnded(RunEndReason reason)
    {
        LastRunNewBestLoot = LastRunNewBestTime = false;
        if (reason == RunEndReason.Abandoned || reason == RunEndReason.None)
            return;

        PlayerPrefs.SetInt(KeyRuns, Runs + 1);
        if (reason == RunEndReason.TimeUpEscaped)
            PlayerPrefs.SetInt(KeyGetaways, Getaways + 1);
        if (_flow.RescueSuccessCount > 0)
            PlayerPrefs.SetInt(KeyRescues, Rescues + _flow.RescueSuccessCount);

        if (_flow.LootValue > BestLoot)
        {
            LastRunNewBestLoot = _flow.LootValue > 0;
            PlayerPrefs.SetInt(KeyBestLoot, _flow.LootValue);
        }
        if (_flow.TimeSurvived > BestTime + 0.01f)
        {
            LastRunNewBestTime = true;
            PlayerPrefs.SetFloat(KeyBestTime, _flow.TimeSurvived);
        }
        PlayerPrefs.Save();
    }

    private static string BuildResultsLines(GameStartFlow f)
    {
        // RunEnded listeners run before the results screen reads this (same frame, after EndRun).
        string bestLoot = LastRunNewBestLoot ? "<b>NEW BEST LOOT!</b>" : $"Best loot {BestLoot}";
        string bestTime = LastRunNewBestTime && !f.WasSuccessful ? "<b>NEW BEST TIME!</b>" : $"Best time {RuntimeUI.FormatTime(BestTime)}";
        return $"{bestLoot}   •   {bestTime}\nGetaways {Getaways}/{Runs}   •   Rescues {Rescues}";
    }

    public static string MenuSummary()
    {
        if (Runs == 0) return "No runs yet. Get AJ out of the city!";
        return $"Best loot {BestLoot}   •   Best time {RuntimeUI.FormatTime(BestTime)}   •   Getaways {Getaways}/{Runs}";
    }
}
