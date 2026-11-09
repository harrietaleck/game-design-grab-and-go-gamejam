using UnityEngine;

/// <summary>Player preferences (audio, visual effects, display, difficulty), saved with PlayerPrefs.</summary>
public static class GameSettings
{
    public enum DifficultyLevel { Easy, Normal, Hard }

    private const string KeyMaster = "gg_vol_master";
    private const string KeyMusic = "gg_vol_music";
    private const string KeySfx = "gg_vol_sfx";
    private const string KeyFlash = "gg_fx_flash";
    private const string KeyShake = "gg_fx_shake";
    private const string KeyCoins = "gg_fx_coins";
    private const string KeyDifficulty = "gg_difficulty";

    private static float s_master = 1f, s_music = 1f, s_sfx = 1f;
    private static bool s_flash = true, s_shake = true, s_coins = true;
    private static DifficultyLevel s_difficulty = DifficultyLevel.Normal;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Load()
    {
        s_master = PlayerPrefs.GetFloat(KeyMaster, 1f);
        s_music = PlayerPrefs.GetFloat(KeyMusic, 1f);
        s_sfx = PlayerPrefs.GetFloat(KeySfx, 1f);
        s_flash = PlayerPrefs.GetInt(KeyFlash, 1) != 0;
        s_shake = PlayerPrefs.GetInt(KeyShake, 1) != 0;
        s_coins = PlayerPrefs.GetInt(KeyCoins, 1) != 0;
        s_difficulty = (DifficultyLevel)Mathf.Clamp(PlayerPrefs.GetInt(KeyDifficulty, 1), 0, 2);
        AudioListener.volume = s_master;
    }

    public static float MasterVolume
    {
        get => s_master;
        set { s_master = Mathf.Clamp01(value); AudioListener.volume = s_master; PlayerPrefs.SetFloat(KeyMaster, s_master); }
    }

    /// <summary>City ambience and menu background audio.</summary>
    public static float MusicVolume
    {
        get => s_music;
        set { s_music = Mathf.Clamp01(value); PlayerPrefs.SetFloat(KeyMusic, s_music); }
    }

    public static float SfxVolume
    {
        get => s_sfx;
        set { s_sfx = Mathf.Clamp01(value); PlayerPrefs.SetFloat(KeySfx, s_sfx); }
    }

    public static bool ScreenFlash
    {
        get => s_flash;
        set { s_flash = value; PlayerPrefs.SetInt(KeyFlash, value ? 1 : 0); }
    }

    public static bool ScreenShake
    {
        get => s_shake;
        set { s_shake = value; PlayerPrefs.SetInt(KeyShake, value ? 1 : 0); }
    }

    public static bool CoinEffects
    {
        get => s_coins;
        set { s_coins = value; PlayerPrefs.SetInt(KeyCoins, value ? 1 : 0); }
    }

    public static DifficultyLevel Difficulty
    {
        get => s_difficulty;
        set { s_difficulty = value; PlayerPrefs.SetInt(KeyDifficulty, (int)value); }
    }

    public static void Save() => PlayerPrefs.Save();
}

/// <summary>Applies the chosen difficulty to the police systems at the start of each gameplay scene.</summary>
public class DifficultyApplier : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        GameStartFlow.RegisterBootstrap(flow =>
        {
            if (flow.GetComponent<DifficultyApplier>() == null)
                flow.gameObject.AddComponent<DifficultyApplier>();
        });
    }

    private void Start()
    {
        if (GameSettings.Difficulty == GameSettings.DifficultyLevel.Normal)
        {
            var k9 = GetComponent<PoliceDirector>();
            if (k9 != null)
            {
                k9.maxDogs = 3;
                k9.dogPackGapSeconds = 3f;
                k9.dogFirstDelay = 15f;
                k9.dogRespawnSeconds = 9f;
            }
            return;
        }
        bool easy = GameSettings.Difficulty == GameSettings.DifficultyLevel.Easy;

        var police = GetComponent<PoliceDirector>();
        if (police != null)
        {
            police.pursuerCatchChance = easy ? 0.1f : 0.4f;
            police.pursuerRespawnSeconds = easy ? 14f : 5f;
            police.spawnIntervalRange *= easy ? 1.4f : 0.75f;
            police.dogRespawnSeconds = easy ? 28f : 5f;
            if (!easy)
            {
                police.maxDogs = 5;
                police.dogPackGapSeconds = 1.5f;
                police.dogBiteDamage = 15f;
                police.dogFirstDelay = 8f;
            }
        }

        var roadblocks = GetComponent<RoadblockDirector>();
        if (roadblocks != null)
            roadblocks.spacingRange *= easy ? 1.4f : 0.75f;
    }
}

/// <summary>Leaving the game, in the editor and in a build.</summary>
public static class GameNavigation
{
    public static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
