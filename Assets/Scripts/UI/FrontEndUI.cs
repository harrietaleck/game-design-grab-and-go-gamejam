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
    private const float SplashSeconds = 0.4f;
    private const float TitleSlamSeconds = 0.45f;
    private static bool s_splashShown;

    private static readonly (string title, string body)[] StoryPages =
    {
        ("THE HEIST", "AJ just pulled the score of his life.\nNow every police officer in the city knows his face."),
        ("THE PLAN", "AJ runs. AJ dodges. AJ takes the credit.\nHe has always been the star of his own story."),
        ("YOU ARE NOT THE MAIN CHARACTER", "You are Claire, his getaway partner.\nYou grab what he drops, draw the heat, and drag him out when he gets caught."),
        ("THE GETAWAY VAN", "Five roadblocks. Then the van, and the city is behind you.\nHe will never say it, but without you he does not make it."),
    };

    private static readonly (string title, string body)[] GuidePages =
    {
        ("GAME RULES", "<color=#19E6FF><b>YOU ARE CLAIRE.</b></color> AJ pulled the heist; you keep him out of handcuffs.\n\n<b>Goal:</b> break through <b>5 roadblocks</b> and reach the <b>getaway van</b> with Claire alive and AJ free.\n\nGrab the loot AJ drops. Dodge traffic, roadblocks and police.\nIf Claire runs out of health, or you fail to rescue AJ, the run is over."),
        ("CONTROLS", "<b>A / D</b> or <b>LEFT / RIGHT</b>   change lane\n<b>W</b> or <b>UP</b>   jump     <b>S</b> or <b>DOWN</b>   dodge\n<b>SPACE</b> or <b>SHIFT</b>   Distraction Pulse (3 per run)\n<b>1 / 2 / 3</b>   answer AJ on the radio\n<b>ESC</b> or <b>P</b>   pause\n\n<color=#19E6FF><b>GAMEPAD</b></color>   stick or d-pad move, up jump, down dodge\n<b>A</b> jump   <b>B</b> dodge   <b>X</b> distract   <b>LB / Y / RB</b> radio   <b>START</b> pause"),
        ("JUMP, DODGE AND SKATE", "<b>Jump</b> spike strips and bins. <b>Slide</b> under barrier arms. Either move makes bullets and grabs miss.\n\nRun over a <b>skateboard</b> and Claire and AJ both ride, faster. The board takes the hit from one crash.\n\nRuns earn <b>points</b>. Spend them in the <b>SHOP</b>."),
        ("LOOT AND CRASHES", "AJ drops coins (+1), gold stacks (+5) and rare jewelry (+10 to +50). Run over them.\n\nEvery crash costs health, spills a quarter of your loot and raises <b>POLICE PRESSURE</b>. It cools when things stay calm.\n\nA hit gives you a second of protection. <b>First-aid kits</b> wait past each roadblock."),
        ("ROADBLOCKS AND GUNFIRE", "<b>Roadblocks</b> close three lanes, and the banner shows the open one. Hitting a car costs 20 health and a quarter of your loot.\n\nGuards fire down their own lanes and grab anyone who arrives late.\n\nA <b>red laser</b> means bullets are coming. Change lane to dodge. Each hit costs 5 health.\n\n<b>Tear gas</b> fills one lane. It slows you and clouds your view, so go around it."),
        ("POLICE ON YOUR TAIL", "A <b>pursuer</b> grabs for 15 health. Dodge him and he goes after AJ.\n\nA <b>K9</b> bites for 10 health. Change lane and it runs past.\n\nWatch for the BEHIND warning.\n\n<b>Distraction Pulse</b> stuns every officer and cools the pressure."),
        ("SAVING AJ", "If AJ is caught he stops in his lane.\nGet into <b>his lane</b> before you reach him and he is free.\nMiss him and the run is over.\n\n<b>Reach the van</b> with Claire alive and AJ free to escape."),
    };

    private const string CreditsText =
        "<color=#19E6FF><b>DESIGN, DIRECTION AND DEVELOPMENT</b></color>\nHarriet Manda\n\n" +
        "<color=#19E6FF><b>THIRD-PARTY ASSETS</b></color>\n" +
        "Cartoon City Free (ithappy)  -  Devtoid Gold Coins  -  LowPoly Fantasy Bags\n" +
        "Police Officer pack  -  Police Car & Helicopter pack\n" +
        "German Shepherd (RetroStyle Games)  -  Modern Guns: Handgun (Nokobot)  -  Jewelry Shop\n" +
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
    private Image _backdrop;
    private Image _sirenLeft, _sirenRight;
    private RectTransform _splashArt;
    private float _splashStartedAt;

    private void Awake()
    {
        Time.timeScale = 1f;
        gameObject.AddComponent<GamepadMenuFocus>();

        _backdrop = RuntimeUI.CreatePanel(transform, "Backdrop", new Color(0.01f, 0.03f, 0.08f, 0.5f));
        _backdrop.raycastTarget = false;
        RuntimeUI.Stretch(_backdrop.rectTransform);

        BuildSplash();
        BuildMain();
        BuildSelect();
        BuildPaged("Story", out _storyTitle, out _storyBody, out _storyPrev, out var storyNext, StoryPrev, StoryNext, CloseStory, "SKIP");
        _storyNextLabel = storyNext.GetComponentInChildren<TextMeshProUGUI>();
        BuildPaged("Guide", out _guideTitle, out _guideBody, out _guidePrev, out _guideNext, GuidePrev, GuideNext, ShowMain, "BACK");
        BuildCredits();
        BuildLoading();
        var shop = NewScreen("Shop");
        shop.gameObject.AddComponent<ShopScreen>().Build(ShowMain);
        Register("Shop", shop.gameObject, null);
        Register("Settings", SharedScreens.BuildSettings(transform, ShowMain), null);
        Register("Scoreboard", SharedScreens.BuildScoreboard(transform, ShowMain, allowClear: true), null);

        if (s_splashShown)
        {
            ShowMain();
        }
        else
        {
            s_splashShown = true;
            _splashStartedAt = Time.unscaledTime;
            _splashEndsAt = _splashStartedAt + SplashSeconds;
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
    }

    private void Update()
    {
        if (_ambience != null) _ambience.volume = 0.4f * GameSettings.MusicVolume;

        if (_current == "Splash")
        {
            AnimateSplash();
            // The art says PRESS ANY KEY, so it waits. (A short lock stops a held key skipping it.)
            if (Time.unscaledTime >= _splashEndsAt && (Input.anyKeyDown || GameInput.AnyPadPressed)) ShowMain();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape) || GameInput.BackPad) GoBack();
    }

    /// <summary>The title slams in, the details fade up, and red and blue police light washes the screen edges.</summary>
    private void AnimateSplash()
    {
        float elapsed = Time.unscaledTime - _splashStartedAt;
        float slam = Mathf.Clamp01(elapsed / TitleSlamSeconds);
        float eased = 1f - (1f - slam) * (1f - slam) * (1f - slam);
        // A quick settle, then a slow push-in so the still image feels alive.
        _splashArt.localScale = Vector3.one * Mathf.Lerp(1.08f, 1f, eased);

        float strength = GameSettings.ScreenFlash ? 0.1f : 0f;
        bool red = Mathf.Repeat(elapsed * 2.5f, 1f) < 0.5f;
        _sirenLeft.color = new Color(1f, 0.08f, 0.1f, red ? strength : 0f);
        _sirenRight.color = new Color(0.1f, 0.3f, 1f, red ? 0f : strength);
    }

    private void GoBack()
    {
        switch (_current)
        {
            case "Story": CloseStory(); break;
            case "Guide":
            case "Shop":
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
        float dim = name == "Splash" ? 0.05f : name == "Main" ? 0.3f : name == "Shop" ? 0.35f : 0.6f;
        _backdrop.color = new Color(0.01f, 0.03f, 0.08f, dim);
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
        _splashArt = AddFullScreenArt(root);
        // The art carries the logo and credit at its edges, so the splash shows all of it at any window shape.
        _splashArt.GetComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.FitInParent;

        _sirenLeft = RuntimeUI.CreatePanel(root, "SirenLeft", Color.clear);
        _sirenLeft.raycastTarget = false;
        _sirenLeft.rectTransform.anchorMin = Vector2.zero;
        _sirenLeft.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        _sirenLeft.rectTransform.offsetMin = _sirenLeft.rectTransform.offsetMax = Vector2.zero;
        _sirenRight = RuntimeUI.CreatePanel(root, "SirenRight", Color.clear);
        _sirenRight.raycastTarget = false;
        _sirenRight.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        _sirenRight.rectTransform.anchorMax = Vector2.one;
        _sirenRight.rectTransform.offsetMin = _sirenRight.rectTransform.offsetMax = Vector2.zero;

        Register("Splash", root.gameObject, null);
    }

    /// <summary>The title art, filling the screen at any aspect ratio without stretching.</summary>
    private static RectTransform AddFullScreenArt(Transform parent)
    {
        var image = RuntimeUI.CreatePanel(parent, "Title Art", Color.white);
        image.raycastTarget = false;
        image.sprite = Resources.Load<Sprite>("SplashArt");
        var fitter = image.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = 1672f / 941f;
        return image.rectTransform;
    }

    private void BuildMain()
    {
        var root = NewScreen("Main");
        FancyText.Create(root, "Title", "GRAB & GO", 130f, UITheme.Fancy.Gold, Top, new Vector2(1500f, 200f), new Vector2(0f, -50f));
        FancyText.Create(root, "Subtitle", "LOOT ON THE RUN", 50f, UITheme.Fancy.Neon, Top, new Vector2(1200f, 90f), new Vector2(0f, -235f));

        var labels = new (string label, UnityEngine.Events.UnityAction action)[]
        {
            ("PLAY", OnPlay), ("SHOP", () => ShowScreen("Shop")), ("STORY", OpenStoryFromMenu), ("HOW TO PLAY", OpenGuide), ("SCOREBOARD", () => ShowScreen("Scoreboard")),
            ("SETTINGS", () => ShowScreen("Settings")), ("CREDITS", () => ShowScreen("Credits")), ("QUIT", GameNavigation.Quit)
        };
        Button first = null;
        for (int i = 0; i < labels.Length; i++)
        {
            var b = RuntimeUI.CreateButton(root, labels[i].label + " Button", labels[i].label, labels[i].action);
            RuntimeUI.Place((RectTransform)b.transform, Top, new Vector2(560f, 78f), new Vector2(0f, -340f - i * 87f));
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
            (GameSettings.DifficultyLevel.Easy, "EASY", "<b>Clear morning.</b>\nFewer police.\nRoadblocks further apart.\nAJ rarely gets caught."),
            (GameSettings.DifficultyLevel.Normal, "NORMAL", "<b>Late afternoon.</b>\nThe getaway as designed."),
            (GameSettings.DifficultyLevel.Hard, "HARD", "<b>Night chase.</b>\nMore police, sooner.\nRoadblocks hit harder.\nAJ needs saving often."),
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
            RuntimeUI.FitInBox(body);
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
        RuntimeUI.FitInBox(body);

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
        RuntimeUI.FitInBox(text);
        var back = RuntimeUI.CreateButton(card.transform, "BackButton", "BACK", ShowMain);
        RuntimeUI.Place((RectTransform)back.transform, Bottom, new Vector2(360f, 84f), new Vector2(0f, 46f));
        Register("Credits", card.gameObject, back);
    }

    private void BuildLoading()
    {
        var root = NewScreen("Loading");
        AddFullScreenArt(root);
        var dim = RuntimeUI.CreatePanel(root, "Dim", new Color(0.01f, 0.03f, 0.08f, 0.6f));
        dim.raycastTarget = false;
        RuntimeUI.Stretch(dim.rectTransform);
        FancyText.Create(root, "Title", "LOADING", 90f, UITheme.Fancy.Neon, Centre, new Vector2(1200f, 150f), new Vector2(0f, 120f));
        _loadingFill = RuntimeUI.CreateBar(root, "Bar", new Color(0f, 0f, 0f, 0.55f), RuntimeUI.Accent);
        RuntimeUI.Place((RectTransform)_loadingFill.transform.parent, Centre, new Vector2(1100f, 44f), new Vector2(0f, -20f));
        _loadingTip = RuntimeUI.CreateText(root, "Tip", "", 30f, TextAlignmentOptions.Center, Color.white);
        RuntimeUI.Place(_loadingTip.rectTransform, Centre, new Vector2(1500f, 70f), new Vector2(0f, -110f));
        Register("Loading", root.gameObject, null);
    }
}
