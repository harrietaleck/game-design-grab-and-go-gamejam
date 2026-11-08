using TMPro;
using UnityEngine;

/// <summary>
/// First-run onboarding (Task 7.4): short timed tips during the player's first run only.
/// Never blocks input, hides during danger (rescue window), and is marked done when the first run ends.
/// </summary>
public class OnboardingTips : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        GameStartFlow.RegisterBootstrap(flow =>
        {
            if (flow.GetComponent<OnboardingTips>() == null)
                flow.gameObject.AddComponent<OnboardingTips>();
        });
    }

    private const string DoneKey = "gg_onboarding_done";

    private static readonly (float at, string text)[] Tips =
    {
        (1.0f,  "A / D change lanes, W jumps, S dodges"),
        (7.0f,  "Run over the loot AJ drops"),
        (14.0f, "Crashes raise POLICE PRESSURE"),
        (20.0f, "SPACE: Distraction Pulse cools the police"),
        (28.0f, "If AJ is caught: get in his lane to save him"),
        (36.0f, "Red laser? Bullets are coming: change lane"),
        (44.0f, "BEHIND warning: a chaser or K9 is on you, change lane"),
        (52.0f, "Roadblock: the banner shows the open lane"),
        (60.0f, "Grab a skateboard: it soaks up one crash"),
        (68.0f, "Spikes: jump. Barrier arm: slide."),
    };
    private const float TipSeconds = 4.5f;

    private GameStartFlow _flow;
    private GameObject _box;
    private TextMeshProUGUI _text;
    private bool _active;
    private int _next;
    private float _hideAt;

    private void Awake()
    {
        _flow = GetComponent<GameStartFlow>();
        Transform root = _flow != null && _flow.hudPanel != null ? _flow.hudPanel.transform : transform;
        var panel = RuntimeUI.CreateHudPanel(root, "OnboardingTip (runtime)");
        RuntimeUI.Place(panel.rectTransform, new Vector2(1f, 0f), new Vector2(820f, 84f), new Vector2(-16f, 215f));
        SidePopup.Attach(panel.rectTransform, fromRight: true);
        _text = RuntimeUI.CreateText(panel.transform, "Text", "", 26f, TextAlignmentOptions.Center, RuntimeUI.Accent, FontStyles.Bold);
        RuntimeUI.Stretch(_text.rectTransform, 24f);
        RuntimeUI.FitOnOneLine(_text);
        _box = panel.gameObject;
        _box.SetActive(false);
    }

    private void OnEnable()
    {
        GameEvents.OnEvent += OnGameEvent;
        if (_flow != null) _flow.RunEnded += OnRunEnded;
    }

    private void OnDisable()
    {
        GameEvents.OnEvent -= OnGameEvent;
        if (_flow != null) _flow.RunEnded -= OnRunEnded;
    }

    private void OnGameEvent(GameEventData e)
    {
        if (e.type == GameEventType.RunStarted)
        {
            _active = PlayerPrefs.GetInt(DoneKey, 0) == 0;
            _next = 0;
        }
    }

    private void OnRunEnded(RunEndReason reason)
    {
        _active = false;
        _box.SetActive(false);
        if (reason != RunEndReason.Abandoned)
        {
            PlayerPrefs.SetInt(DoneKey, 1);
            PlayerPrefs.Save();
        }
    }

    private void Update()
    {
        if (!_active || _flow == null) return;
        if (_flow.State == GameStartFlow.RunState.AJCaught) { _box.SetActive(false); return; }
        if (_flow.State != GameStartFlow.RunState.Playing) return;

        float t = _flow.TimeSurvived;
        if (_next < Tips.Length && t >= Tips[_next].at)
        {
            _text.text = _next == 0 && GameInput.UsingGamepad ? "Stick moves, A jumps, B dodges" : Tips[_next].text;
            _box.SetActive(true);
            _hideAt = t + TipSeconds;
            _next++;
        }
        if (_box.activeSelf && t >= _hideAt)
        {
            _box.SetActive(false);
            if (_next >= Tips.Length) _active = false;
        }
    }

    /// <summary>Lets players see the tips again on their next run.</summary>
    public static void ResetOnboarding() => PlayerPrefs.DeleteKey(DoneKey);
}
