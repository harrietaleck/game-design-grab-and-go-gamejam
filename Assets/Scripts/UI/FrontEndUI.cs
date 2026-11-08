using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The front-end scene: splash, main menu, difficulty select, story, guide, settings, scoreboard, credits and
/// the loading screen. Everything is built at runtime with RuntimeUI so it matches the gameplay screens.
/// </summary>
public class FrontEndUI : MonoBehaviour
{
    private static readonly Vector2 Centre = new Vector2(0.5f, 0.5f);
    private static readonly Vector2 Top = new Vector2(0.5f, 1f);
    private static readonly Vector2 Bottom = new Vector2(0.5f, 0f);

    private const string StorySeenKey = "gg_story_seen";
    private const float SplashSeconds = 2.8f;
    private static bool s_splashShown;

    private static readonly (string title, string body)[] StoryPages =
    {
        ("THE HEIST", "AJ just pulled the score of his life.\nNow every police officer in the city knows his face."),
        ("THE PLAN", "AJ runs. AJ dodges. AJ takes the credit.\nHe has always been the star of his own story."),
        ("YOU ARE NOT THE MAIN CHARACTER", "You are Claire, his getaway partner.\nYou grab what he drops, draw the heat, and drag him out when he gets caught."),
        ("TWO MINUTES", "Survive two minutes and the city is behind you.\nHe will never say it, but without you he does not make it."),
    };

    private static readonly (string title, string body)[] GuidePages =
    {
        ("CONTROLS", "<b>A / D</b> or <b>LEFT / RIGHT</b>   change lane\n<b>SPACE</b> or <b>SHIFT</b>   Distraction Pulse (3 per run)\n<b>1 / 2 / 3</b>   answer AJ on the radio\n<b>ESC</b> or <b>P</b>   pause"),
        ("LOOT AND CRASHES", "AJ drops coins (+1) and gold stacks (+5). Run over them.\n\nEvery crash costs health, spills half your loot and raises <b>POLICE PRESSURE</b>. It cools when things stay calm."),
        ("THE POLICE", "<b>Roadblocks</b> close three lanes. The banner shows the open one, and guards grab anyone who arrives late.\n\nA <b>pursuer</b> comes up behind you. Change lane when POLICE BEHIND shows.\n\n<b>Distraction Pulse</b> stuns every officer and cools the pressure."),
        ("SAVING AJ", "If AJ is caught he stops in his lane.\nGet into <b>his lane</b> before you reach him and he is free.\nMiss him and the run is over.\n\n<b>Survive 2:00</b> with Claire alive and AJ free to escape."),
    };

    private const string CreditsText =
        "<color=#19E6FF><b>DESIGN, DIRECTION AND DEVELOPMENT</b></color>\nHarriet Maleck\n\n" +
        "<color=#19E6FF><b>THIRD-PARTY ASSETS</b></color>\n" +
        "Cartoon City Free (ithappy)  -  Devtoid Gold Coins  -  LowPoly Fantasy Bags\n" +
        "Police Officer pack  -  Police Car & Helicopter pack\n" +
        "Space Exploration GUI Kit  -  Sci-Fi UI  -  MK Easy Text\n" +
        "Mixamo animations  -  Mixkit sounds  -  ElevenLabs voice clip\n\n" +
        "<color=#19E6FF><b>AI ASSISTANCE</b></color>\nBuilt with Cursor and Claude, as disclosed in the README";

    [Tooltip("Looping background audio for the menus (set by the scene builder).")]
    public AudioClip menuAmbience;

    private readonly Dictionary<string, GameObject> _screens = new();
    private readonly Dictionary<string, Selectable> _firstSelected = new();
    private string _current;
    private float _splashEndsAt;
    private bool _loading;
    private bool _storyContinuesToGame;
    private int _storyPage;
    private int _guidePage;

    private FancyText _storyTitle, _guideTitle;
    private TextMeshProUGUI _storyBody, _guideBody, _storyNextLabel, _bests, _loadingTip;
    private Button _guidePrev, _guideNext, _storyPrev;
    private Image _loadingFill;
    private AudioSource _ambience;
    private Transform _helicopter;
    private Vector3 _helicopterBase;

    private void Awake()
    {
        Time.timeScale = 1f;

        var dim = RuntimeUI.CreatePanel(transform, "Backdrop", new Color(0.01f, 0.03f, 0.08f, 0.5f));
        dim.raycastTarget = false;
        RuntimeUI.Stretch(dim.rectTransform);

        BuildSplash();
        BuildMain();
        BuildSelect();
        BuildPaged("Story", out _storyTitle, out _storyBody, out _storyPrev, out var storyNext, StoryPrev, StoryNext, CloseStory, "SKIP");
        _storyNextLabel = storyNext.GetComponentInChildren<TextMeshProUGUI>();
        BuildPaged("Guide", out _guideTitle, out _guideBody, out _guidePrev, out _guideNext, GuidePrev, GuideNext, ShowMain, "BACK");
        BuildCredits();
        BuildLoading();
        Register("Settings", SharedScreens.BuildSettings(transform, ShowMain), null);
        Register("Scoreboard", SharedScreens.BuildScoreboard(transform, ShowMain, allowClear: true), null);

        if (s_splashShown)
        {
            ShowMain();
        }
        else
        {
            s_splashShown = true;
            _splashEndsAt = Time.unscaledTime + SplashSeconds;
            ShowScreen("Splash");
        }
    }

    private void Start()
    {
        if (menuAmbience != null)
        {
            _ambience = gameObject.AddComponent<AudioSource>();
            _ambience.clip = menuAmbience;
            _ambience.loop = true;
            _ambience.volume = 0.4f * GameSettings.MusicVolume;
            _ambience.Play();
        }

        var set = Resources.Load<PoliceSet>("PoliceSet");
        if (set != null && set.helicopter != null)
        {
            var heli = Instantiate(set.helicopter, new Vector3(-30f, 7f, 34f), Quaternion.Euler(6f, 150f, 8f));
            foreach (var c in heli.GetComponentsInChildren<Collider>(true)) Destroy(c);
            _helicopter = heli.transform;
            _helicopterBase = _helicopter.position;
        }
    }

    private void Update()
    {
        if (_ambience != null) _ambience.volume = 0.4f * GameSettings.MusicVolume;
        if (_helicopter != null)
            _helicopter.position = _helicopterBase + new Vector3(Mathf.Sin(Time.time * 0.3f) * 3f, Mathf.Sin(Time.time * 0.8f) * 0.6f, 0f);

        if (_current == "Splash")
        {
            if (Time.unscaledTime >= _splashEndsAt || Input.anyKeyDown) ShowMain();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape)) GoBack();
    }

    private void GoBack()
    {
        switch (_current)
        {
            case "Story": CloseStory(); break;
            case "Guide":
            case "Select":
            case "Settings":
            case "Scoreboard":
            case "Credits":
                GameSettings.Save();
                ShowMain();
                break;
        }
    }

    // ---------------- Navigation ----------------

    private void Register(string name, GameObject screen, Selectable first)
    {
        screen.SetActive(false);
        _screens[name] = screen;
        _firstSelected[name] = first;
    }

    private void ShowScreen(string name)
    {
        foreach (var pair in _screens) pair.Value.SetActive(pair.Key == name);
        _current = name;
        if (_firstSelected.TryGetValue(name, out var first) && first != null) first.Select();
    }

    private void ShowMain()
    {
        if (_bests != null) _bests.text = ProgressionStore.MenuSummary();
        ShowScreen("Main");
    }

    private void OnPlay() => ShowScreen("Select");

    private void OnDifficultyChosen(GameSettings.DifficultyLevel level)
    {
        GameSettings.Difficulty = level;
        GameSettings.Save();

        if (PlayerPrefs.GetInt(StorySeenKey, 0) == 0)
        {
            _storyContinuesToGame = true;
            OpenStory();
        }
        else
        {
            StartCoroutine(LoadGame());
        }
    }

    private void OpenStoryFromMenu()
    {
        _storyContinuesToGame = false;
        OpenStory();
    }

    private void OpenStory()
    {
        _storyPage = 0;
        RefreshStory();
        ShowScreen("Story");
    }

    private void StoryPrev()
    {
        _storyPage = Mathf.Max(0, _storyPage - 1);
        RefreshStory();
    }

    private void StoryNext()
    {
        if (_storyPage >= StoryPages.Length - 1)
        {
            CloseStory();
            return;
        }
        _storyPage++;
        RefreshStory();
    }

    private void RefreshStory()
    {
        _storyTitle.SetText(StoryPages[_storyPage].title);
        _storyBody.text = StoryPages[_storyPage].body;
        _storyPrev.gameObject.SetActive(_storyPage > 0);
        bool last = _storyPage == StoryPages.Length - 1;
        _storyNextLabel.text = last ? (_storyContinuesToGame ? "START RUN" : "DONE") : "NEXT";
    }

    private void CloseStory()
    {
        PlayerPrefs.SetInt(StorySeenKey, 1);
        PlayerPrefs.Save();
        if (_storyContinuesToGame) StartCoroutine(LoadGame());
        else ShowMain();
    }

    private void OpenGuide()
    {
        _guidePage = 0;
        RefreshGuide();
        ShowScreen("Guide");
    }

    private void GuidePrev()
    {
        _guidePage = Mathf.Max(0, _guidePage - 1);
        RefreshGuide();
    }

    private void GuideNext()
    {
        _guidePage = Mathf.Min(GuidePages.Length - 1, _guidePage + 1);
        RefreshGuide();
    }

    private void RefreshGuide()
    {
        _guideTitle.SetText(GuidePages[_guidePage].title);
        _guideBody.text = GuidePages[_guidePage].body;
        _guidePrev.gameObject.SetActive(_guidePage > 0);
        _guideNext.gameObject.SetActive(_guidePage < GuidePages.Length - 1);
    }

    private IEnumerator LoadGame()
    {
        if (_loading) yield break;
        _loading = true;
        ShowScreen("Loading");
        _loadingFill.fillAmount = 0f;
        _loadingTip.text = "Tip: Distraction Pulse stuns every officer on the street.";
        yield return null;

        GameStartFlow.RequestAutoStart();
        var op = SceneManager.LoadSceneAsync(GameStartFlow.GameSceneName);
        op.allowSceneActivation = false;
        while (op.progress < 0.9f)
        {
            _loadingFill.fillAmount = Mathf.Clamp01(op.progress / 0.9f);
            yield return null;
        }
        _loadingFill.fillAmount = 1f;
        yield return new WaitForSecondsRealtime(0.3f);
        op.allowSceneActivation = true;
    }

    // ---------------- Building ----------------

    private RectTransform NewScreen(string name)
    {
        var rt = RuntimeUI.CreateRect(transform, name);
        RuntimeUI.Stretch(rt);
        return rt;
    }

    private void BuildSplash()
    {
        var root = NewScreen("Splash");
        FancyText.Create(root, "Title", "GRAB & GO", 170f, UITheme.Fancy.Gold, Centre, new Vector2(1700f, 260f), new Vector2(0f, 90f));
        FancyText.Create(root, "Subtitle", "LOOT ON THE RUN", 70f, UITheme.Fancy.Neon, Centre, new Vector2(1500f, 120f), new Vector2(0f, -80f));
        var by = RuntimeUI.CreateText(root, "Credit", "A game by Harriet Maleck", 30f, TextAlignmentOptions.Center, Color.white);
        RuntimeUI.Place(by.rectTransform, Bottom, new Vector2(1200f, 60f), new Vector2(0f, 90f));
        Register("Splash", root.gameObject, null);
    }

    private void BuildMain()
    {
        var root = NewScreen("Main");
        FancyText.Create(root, "Title", "GRAB & GO", 130f, UITheme.Fancy.Gold, Top, new Vector2(1500f, 200f), new Vector2(0f, -50f));
        FancyText.Create(root, "Subtitle", "LOOT ON THE RUN", 50f, UITheme.Fancy.Neon, Top, new Vector2(1200f, 90f), new Vector2(0f, -235f));

        var labels = new (string label, UnityEngine.Events.UnityAction action)[]
        {
            ("PLAY", OnPlay), ("STORY", OpenStoryFromMenu), ("HOW TO PLAY", OpenGuide), ("SCOREBOARD", () => ShowScreen("Scoreboard")),
            ("SETTINGS", () => ShowScreen("Settings")), ("CREDITS", () => ShowScreen("Credits")), ("QUIT", GameNavigation.Quit)
        };
        Button first = null;
        for (int i = 0; i < labels.Length; i++)
        {
            var b = RuntimeUI.CreateButton(root, labels[i].label + " Button", labels[i].label, labels[i].action);
            RuntimeUI.Place((RectTransform)b.transform, Top, new Vector2(560f, 84f), new Vector2(0f, -345f - i * 96f));
            if (first == null) first = b;
        }

        _bests = RuntimeUI.CreateText(root, "Bests", "", 28f, TextAlignmentOptions.Center, RuntimeUI.Gold, FontStyles.Bold);
        RuntimeUI.Place(_bests.rectTransform, Bottom, new Vector2(1800f, 60f), new Vector2(0f, 24f));
        Register("Main", root.gameObject, first);
    }

    private void BuildSelect()
    {
        var root = NewScreen("Select");
        FancyText.Create(root, "Title", "SELECT DIFFICULTY", 90f, UITheme.Fancy.Neon, Top, new Vector2(1700f, 150f), new Vector2(0f, -90f));

        var options = new (GameSettings.DifficultyLevel level, string title, string body)[]
        {
            (GameSettings.DifficultyLevel.Easy, "EASY", "Fewer police.\nRoadblocks further apart.\nAJ rarely gets caught."),
            (GameSettings.DifficultyLevel.Normal, "NORMAL", "The getaway as designed."),
            (GameSettings.DifficultyLevel.Hard, "HARD", "More police, sooner.\nRoadblocks hit harder.\nAJ needs saving often."),
        };

        Button first = null;
        for (int i = 0; i < options.Length; i++)
        {
            var option = options[i];
            float x = (i - 1) * 590f;
            var card = RuntimeUI.CreateWindow(root, option.title + " Card", null, 0.8f);
            card.raycastTarget = false;
            RuntimeUI.Place(card.rectTransform, Centre, new Vector2(540f, 560f), new Vector2(x, 10f));
            var body = RuntimeUI.CreateText(card.transform, "Body", option.body, 30f, TextAlignmentOptions.Center, Color.white);
            RuntimeUI.Place(body.rectTransform, Top, new Vector2(470f, 300f), new Vector2(0f, -60f));
            var pick = RuntimeUI.CreateButton(card.transform, "Select " + option.title, option.title, () => OnDifficultyChosen(option.level));
            RuntimeUI.Place((RectTransform)pick.transform, Bottom, new Vector2(400f, 96f), new Vector2(0f, 50f));
            if (option.level == GameSettings.DifficultyLevel.Normal) first = pick;
        }

        var back = RuntimeUI.CreateButton(root, "BackButton", "BACK", ShowMain);
        RuntimeUI.Place((RectTransform)back.transform, Bottom, new Vector2(360f, 84f), new Vector2(0f, 70f));
        Register("Select", root.gameObject, first);
    }

    private void BuildPaged(string name, out FancyText title, out TextMeshProUGUI body, out Button prev, out Button next,
                            UnityEngine.Events.UnityAction onPrev, UnityEngine.Events.UnityAction onNext,
                            UnityEngine.Events.UnityAction onLeave, string leaveLabel)
    {
        var card = RuntimeUI.CreateWindow(transform, name, null, 0.82f);
        RuntimeUI.Place(card.rectTransform, Centre, new Vector2(1500f, 960f), Vector2.zero);
        title = FancyText.Create(card.transform, "Title", "", 60f, UITheme.Fancy.Gold, Top, new Vector2(1420f, 110f), new Vector2(0f, -44f));
        body = RuntimeUI.CreateText(card.transform, "Body", "", 36f, TextAlignmentOptions.Center, Color.white);
        RuntimeUI.Place(body.rectTransform, Top, new Vector2(1300f, 560f), new Vector2(0f, -180f));

        prev = RuntimeUI.CreateButton(card.transform, "PrevButton", "PREVIOUS", onPrev);
        RuntimeUI.Place((RectTransform)prev.transform, Bottom, new Vector2(380f, 84f), new Vector2(-430f, 50f));
        var leave = RuntimeUI.CreateButton(card.transform, "LeaveButton", leaveLabel, onLeave, new Color(0.85f, 0.85f, 0.9f));
        RuntimeUI.Place((RectTransform)leave.transform, Bottom, new Vector2(380f, 84f), new Vector2(0f, 50f));
        next = RuntimeUI.CreateButton(card.transform, "NextButton", "NEXT", onNext);
        RuntimeUI.Place((RectTransform)next.transform, Bottom, new Vector2(380f, 84f), new Vector2(430f, 50f));
        Register(name, card.gameObject, next);
    }

    private void BuildCredits()
    {
        var card = RuntimeUI.CreateWindow(transform, "Credits", null, 0.82f);
        RuntimeUI.Place(card.rectTransform, Centre, new Vector2(1500f, 960f), Vector2.zero);
        FancyText.Create(card.transform, "Title", "CREDITS", 64f, UITheme.Fancy.Neon, Top, new Vector2(1420f, 110f), new Vector2(0f, -40f));
        var text = RuntimeUI.CreateText(card.transform, "Body", CreditsText, 30f, TextAlignmentOptions.Center, Color.white);
        RuntimeUI.Place(text.rectTransform, Top, new Vector2(1380f, 640f), new Vector2(0f, -160f));
        var back = RuntimeUI.CreateButton(card.transform, "BackButton", "BACK", ShowMain);
        RuntimeUI.Place((RectTransform)back.transform, Bottom, new Vector2(360f, 84f), new Vector2(0f, 46f));
        Register("Credits", card.gameObject, back);
    }

    private void BuildLoading()
    {
        var root = NewScreen("Loading");
        FancyText.Create(root, "Title", "LOADING", 90f, UITheme.Fancy.Neon, Centre, new Vector2(1200f, 150f), new Vector2(0f, 120f));
        _loadingFill = RuntimeUI.CreateBar(root, "Bar", new Color(0f, 0f, 0f, 0.55f), RuntimeUI.Accent);
        RuntimeUI.Place((RectTransform)_loadingFill.transform.parent, Centre, new Vector2(1100f, 44f), new Vector2(0f, -20f));
        _loadingTip = RuntimeUI.CreateText(root, "Tip", "", 30f, TextAlignmentOptions.Center, Color.white);
        RuntimeUI.Place(_loadingTip.rectTransform, Centre, new Vector2(1500f, 70f), new Vector2(0f, -110f));
        Register("Loading", root.gameObject, null);
    }
}
