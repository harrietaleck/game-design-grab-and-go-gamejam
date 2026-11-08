using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>Screens used both in the front-end scene and on top of gameplay: settings and the scoreboard.</summary>
public static class SharedScreens
{
    private static readonly Vector2 Centre = new Vector2(0.5f, 0.5f);
    private static readonly Vector2 Top = new Vector2(0.5f, 1f);
    private static readonly Vector2 Bottom = new Vector2(0.5f, 0f);

    private static Image Card(Transform parent, string name, string title, Vector2 size)
    {
        var card = RuntimeUI.CreateWindow(parent, name, null, 0.82f);
        RuntimeUI.Place(card.rectTransform, Centre, size, Vector2.zero);
        FancyText.Create(card.transform, "Title", title, 64f, UITheme.Fancy.Neon, Top, new Vector2(size.x - 60f, 100f), new Vector2(0f, -36f));
        return card;
    }

    public static GameObject BuildSettings(Transform parent, UnityAction onBack)
    {
        var card = Card(parent, "Settings (runtime)", "SETTINGS", new Vector2(1320f, 1080f));
        var root = card.transform;

        AddSlider(root, "MASTER", -165f, GameSettings.MasterVolume, v => GameSettings.MasterVolume = v);
        AddSlider(root, "AMBIENCE", -255f, GameSettings.MusicVolume, v => GameSettings.MusicVolume = v);
        AddSlider(root, "SFX", -345f, GameSettings.SfxVolume, v =>
        {
            GameSettings.SfxVolume = v;
            ProceduralSfx.Play(ProceduralSfx.RadioBlip, 0.4f);
        });

        AddSwitch(root, "SCREEN FLASH", -315f, -470f, () => GameSettings.ScreenFlash, v => GameSettings.ScreenFlash = v);
        AddSwitch(root, "SCREEN SHAKE", 315f, -470f, () => GameSettings.ScreenShake, v => GameSettings.ScreenShake = v);
        AddSwitch(root, "COIN EFFECTS", -315f, -570f, () => GameSettings.CoinEffects, v => GameSettings.CoinEffects = v);
        AddSwitch(root, "FULLSCREEN", 315f, -570f, () => Screen.fullScreen, v => Screen.fullScreen = v);

        TextMeshProUGUI qualityLabel = null;
        var quality = AddButton(root, "QualityButton", "", -315f, -670f, 600f, () =>
        {
            int next = (QualitySettings.GetQualityLevel() + 1) % QualitySettings.names.Length;
            QualitySettings.SetQualityLevel(next, true);
            qualityLabel.text = QualityText();
        });
        qualityLabel = quality.GetComponentInChildren<TextMeshProUGUI>();
        qualityLabel.text = QualityText();

        TextMeshProUGUI tipsLabel = null;
        var tips = AddButton(root, "ReplayTipsButton", "REPLAY TIPS", 315f, -670f, 600f, () =>
        {
            OnboardingTips.ResetOnboarding();
            tipsLabel.text = "TIPS WILL REPLAY";
        });
        tipsLabel = tips.GetComponentInChildren<TextMeshProUGUI>();

        var back = RuntimeUI.CreateButton(root, "BackButton", "BACK", () =>
        {
            GameSettings.Save();
            onBack?.Invoke();
        });
        RuntimeUI.Place((RectTransform)back.transform, Bottom, new Vector2(360f, 84f), new Vector2(0f, 42f));
        return card.gameObject;
    }

    private static string QualityText() => $"QUALITY: {QualitySettings.names[QualitySettings.GetQualityLevel()].ToUpperInvariant()}";

    private static void AddSlider(Transform root, string label, float y, float value, UnityAction<float> onChange)
    {
        var text = RuntimeUI.CreateText(root, label + " Label", label, 30f, TextAlignmentOptions.Left, Color.white, FontStyles.Bold);
        RuntimeUI.Place(text.rectTransform, Top, new Vector2(340f, 60f), new Vector2(-400f, y));

        var slider = RuntimeUI.CreateSlider(root, label + " Slider", value);
        RuntimeUI.Place((RectTransform)slider.transform, Top, new Vector2(640f, 44f), new Vector2(90f, y - 8f));

        var percent = RuntimeUI.CreateText(root, label + " Value", "", 30f, TextAlignmentOptions.Right, RuntimeUI.Accent, FontStyles.Bold);
        RuntimeUI.Place(percent.rectTransform, Top, new Vector2(150f, 60f), new Vector2(510f, y));
        percent.text = $"{Mathf.RoundToInt(value * 100f)}%";

        slider.onValueChanged.AddListener(v =>
        {
            percent.text = $"{Mathf.RoundToInt(v * 100f)}%";
            onChange(v);
        });
    }

    private static void AddSwitch(Transform root, string label, float x, float y, System.Func<bool> get, System.Action<bool> set)
    {
        TextMeshProUGUI text = null;
        bool current = get();
        var button = AddButton(root, label + " Button", "", x, y, 600f, () =>
        {
            current = !current;
            set(current);
            text.text = $"{label}: {(current ? "ON" : "OFF")}";
        });
        text = button.GetComponentInChildren<TextMeshProUGUI>();
        text.text = $"{label}: {(current ? "ON" : "OFF")}";
    }

    private static Button AddButton(Transform root, string name, string label, float x, float y, float width, UnityAction onClick)
    {
        var button = RuntimeUI.CreateButton(root, name, label, onClick);
        RuntimeUI.Place((RectTransform)button.transform, Top, new Vector2(width, 84f), new Vector2(x, y));
        return button;
    }

    public static GameObject BuildScoreboard(Transform parent, UnityAction onBack, bool allowClear)
    {
        var card = Card(parent, "Scoreboard (runtime)", "SCOREBOARD", new Vector2(1500f, 1080f));
        var root = card.transform;

        var table = RuntimeUI.CreateText(root, "Table", "", 28f, TextAlignmentOptions.TopLeft, Color.white);
        RuntimeUI.Place(table.rectTransform, Top, new Vector2(1360f, 760f), new Vector2(0f, -150f));
        table.textWrappingMode = TextWrappingModes.NoWrap;
        card.gameObject.AddComponent<ScoreboardView>().table = table;

        var back = RuntimeUI.CreateButton(root, "BackButton", "BACK", onBack);
        RuntimeUI.Place((RectTransform)back.transform, Bottom, new Vector2(360f, 84f), new Vector2(allowClear ? -200f : 0f, 42f));

        if (allowClear)
        {
            TextMeshProUGUI label = null;
            var clear = RuntimeUI.CreateButton(root, "ClearButton", "CLEAR", null, new Color(0.85f, 0.85f, 0.9f));
            label = clear.GetComponentInChildren<TextMeshProUGUI>();
            clear.onClick.AddListener(() =>
            {
                if (label.text != "SURE?")
                {
                    label.text = "SURE?";
                    return;
                }
                ScoreboardStore.Clear();
                label.text = "CLEAR";
                card.GetComponent<ScoreboardView>().Refresh();
            });
            RuntimeUI.Place((RectTransform)clear.transform, Bottom, new Vector2(360f, 84f), new Vector2(200f, 42f));
        }
        return card.gameObject;
    }
}

/// <summary>Fills the scoreboard table whenever it is shown.</summary>
public class ScoreboardView : MonoBehaviour
{
    public TextMeshProUGUI table;

    private void OnEnable() => Refresh();

    public void Refresh()
    {
        if (table == null) return;
        var sb = new StringBuilder();
        sb.Append(Row("#", "LOOT", "TIME", "RESULT", "MODE", "DATE", "#19E6FF"));

        var entries = ScoreboardStore.Load();
        if (entries.Count == 0)
            sb.Append("\nNo runs yet. Get AJ out of the city!");
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            string color = i == ScoreboardStore.LastRank ? "#FFD140" : "#FFFFFF";
            sb.Append(Row((i + 1).ToString(), e.loot.ToString(), RuntimeUI.FormatTime(e.time), e.result, e.difficulty, e.date, color));
        }
        table.text = sb.ToString();
    }

    private static string Row(string rank, string loot, string time, string result, string mode, string date, string color) =>
        $"<color={color}>{rank}<pos=7%>{loot}<pos=19%>{time}<pos=33%>{result}<pos=62%>{mode}<pos=78%>{date}</color>\n";
}
