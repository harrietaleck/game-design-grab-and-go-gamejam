using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Top-10 local scoreboard, saved as JSON in PlayerPrefs. Ranked by loot, then time survived.</summary>
public static class ScoreboardStore
{
    public const int MaxEntries = 10;
    private const string Key = "gg_scoreboard";

    [Serializable]
    public class Entry
    {
        public int loot;
        public float time;
        public string result;
        public string difficulty;
        public string date;
    }

    [Serializable]
    private class Table
    {
        public List<Entry> entries = new();
    }

    /// <summary>Rank (0-based) of the run recorded last, or -1 if it did not make the table.</summary>
    public static int LastRank { get; private set; } = -1;

    public static List<Entry> Load()
    {
        string json = PlayerPrefs.GetString(Key, "");
        if (string.IsNullOrEmpty(json)) return new List<Entry>();
        try
        {
            var table = JsonUtility.FromJson<Table>(json);
            return table != null && table.entries != null ? table.entries : new List<Entry>();
        }
        catch (ArgumentException)
        {
            return new List<Entry>();
        }
    }

    public static void Record(int loot, float time, string result)
    {
        var entries = Load();
        var entry = new Entry
        {
            loot = loot,
            time = time,
            result = result,
            difficulty = GameSettings.Difficulty.ToString().ToUpperInvariant(),
            date = DateTime.Now.ToString("yyyy-MM-dd")
        };

        int index = entries.FindIndex(e => loot > e.loot || (loot == e.loot && time > e.time));
        if (index < 0) index = entries.Count;
        entries.Insert(index, entry);
        if (entries.Count > MaxEntries) entries.RemoveRange(MaxEntries, entries.Count - MaxEntries);
        LastRank = index < MaxEntries ? index : -1;
        Save(entries);
    }

    public static void Clear()
    {
        LastRank = -1;
        PlayerPrefs.DeleteKey(Key);
        PlayerPrefs.Save();
    }

    private static void Save(List<Entry> entries)
    {
        PlayerPrefs.SetString(Key, JsonUtility.ToJson(new Table { entries = entries }));
        PlayerPrefs.Save();
    }
}
