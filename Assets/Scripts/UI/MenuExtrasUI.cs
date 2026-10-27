using TMPro;
using UnityEngine;

/// <summary>
/// Main-menu additions (Task 7.1): a concise how-to-play guide, start keys and personal bests.
/// Added under the existing MenuPanel at runtime; the original title and PLAY button are untouched.
/// </summary>
public class MenuExtrasUI : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        GameStartFlow.RegisterBootstrap(flow =>
        {
            if (flow.GetComponent<MenuExtrasUI>() == null)
                flow.gameObject.AddComponent<MenuExtrasUI>();
        });
    }

    private void Start()
    {
        var flow = GetComponent<GameStartFlow>();
        if (flow == null || flow.menuPanel == null) return;
        Transform root = flow.menuPanel.transform;

        var panel = RuntimeUI.CreatePanel(root, "HowToPlay (runtime)", new Color(0.02f, 0.03f, 0.1f, 0.7f));
        panel.raycastTarget = false;
        RuntimeUI.Place(panel.rectTransform, new Vector2(0.5f, 0f), new Vector2(980f, 250f), new Vector2(0f, 40f));

        var guide = RuntimeUI.CreateText(panel.transform, "Guide",
            "<b><color=#19E6FF>YOU ARE CLAIRE.</color></b> AJ pulled the heist; you keep him out of handcuffs.\n" +
            "<b>A / D</b> or <b>← →</b> change lanes   •   grab the loot AJ drops (gold stacks = 5)\n" +
            "Crashes hurt you and raise <b>POLICE PRESSURE</b>   •   <b>SPACE</b> = Distraction Pulse (3 per run)\n" +
            "If AJ gets caught, get into <b>his lane</b> before you reach him   •   survive <b>2:00</b> to escape\n" +
            "<b>1 / 2 / 3</b> answer AJ on the radio   •   <b>ESC</b> pause",
            22f, TextAlignmentOptions.Center, Color.white);
        RuntimeUI.Place(guide.rectTransform, new Vector2(0.5f, 1f), new Vector2(950f, 170f), new Vector2(0f, -10f));

        var stats = RuntimeUI.CreateText(panel.transform, "Bests", ProgressionStore.MenuSummary(), 22f,
            TextAlignmentOptions.Center, RuntimeUI.Gold, FontStyles.Bold);
        RuntimeUI.Place(stats.rectTransform, new Vector2(0.5f, 0f), new Vector2(950f, 34f), new Vector2(0f, 40f));

        var start = RuntimeUI.CreateText(panel.transform, "StartHint", "Press SPACE / ENTER or click PLAY", 20f,
            TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.75f));
        RuntimeUI.Place(start.rectTransform, new Vector2(0.5f, 0f), new Vector2(950f, 30f), new Vector2(0f, 8f));
    }
}
