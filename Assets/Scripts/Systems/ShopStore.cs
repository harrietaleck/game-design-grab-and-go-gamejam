using UnityEngine;

/// <summary>Points earned from runs, and what they have bought. Saved with PlayerPrefs.</summary>
public static class ShopStore
{
    public const string Sprint = "sprint";
    public const string BigBag = "bigbag";
    public const string DoubleJump = "doublejump";
    public const string ExtraCharge = "charge";
    public const string LongRide = "longride";
    public const string Jacket = "jacket";

    public const float SprintMultiplier = 1.1f;

    private const string KeyPoints = "gg_points";
    private const string KeyOwned = "gg_owned";
    private const string KeyBoard = "gg_board";
    private const string KeyBag = "gg_bag";
    private const string KeyShoes = "gg_shoes";

    public static int Points => PlayerPrefs.GetInt(KeyPoints, 0);

    /// <summary>Points earned by the most recent finished run.</summary>
    public static int LastAward { get; private set; }

    public static void Award(int points)
    {
        LastAward = Mathf.Max(0, points);
        PlayerPrefs.SetInt(KeyPoints, Points + LastAward);
        PlayerPrefs.Save();
    }

    public static bool Owns(string id) => $",{PlayerPrefs.GetString(KeyOwned, "")},".Contains($",{id},");

    /// <summary>Spends points on an item. Returns false if it cannot be afforded.</summary>
    public static bool Buy(string id, int price)
    {
        if (Owns(id)) return true;
        if (Points < price) return false;
        PlayerPrefs.SetInt(KeyPoints, Points - price);
        string owned = PlayerPrefs.GetString(KeyOwned, "");
        PlayerPrefs.SetString(KeyOwned, owned.Length == 0 ? id : $"{owned},{id}");
        PlayerPrefs.Save();
        return true;
    }

    public static int EquippedBoard
    {
        get => PlayerPrefs.GetInt(KeyBoard, 0);
        set { PlayerPrefs.SetInt(KeyBoard, value); PlayerPrefs.Save(); }
    }

    public static int EquippedShoes
    {
        get => PlayerPrefs.GetInt(KeyShoes, 0);
        set { PlayerPrefs.SetInt(KeyShoes, value); PlayerPrefs.Save(); }
    }

    /// <summary>Index into ShopSet.bags, or -1 for Claire's original bag.</summary>
    public static int EquippedBag
    {
        get => PlayerPrefs.GetInt(KeyBag, -1);
        set { PlayerPrefs.SetInt(KeyBag, value); PlayerPrefs.Save(); }
    }
}
