using UnityEngine;

/// <summary>
/// Claire's support ability (Task 2.5): a limited-charge Distraction Pulse.
/// Lowers police pressure and blocks new pressure for a few seconds. It never moves AJ.
/// </summary>
public class DistractionPulse : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        GameStartFlow.RegisterBootstrap(flow =>
        {
            if (flow.GetComponent<DistractionPulse>() == null)
                flow.gameObject.AddComponent<DistractionPulse>();
        });
    }

    public static DistractionPulse Instance { get; private set; }

    public KeyCode[] activateKeys = { KeyCode.Space, KeyCode.LeftShift, KeyCode.RightShift };
    public int chargesPerRun = 3;
    public float cooldownSeconds = 5f;
    [Tooltip("Pressure removed per pulse.")]
    public float pressureRelief = 35f;
    [Tooltip("Seconds during which new pressure is ignored after a pulse.")]
    public float suppressSeconds = 3f;

    public int ChargesLeft { get; private set; }
    public float CooldownRemaining => Mathf.Max(0f, _readyAt - Time.time);
    public float Cooldown01 => cooldownSeconds <= 0f ? 0f : CooldownRemaining / cooldownSeconds;
    public bool IsReady => ChargesLeft > 0 && CooldownRemaining <= 0f && CanUseNow;
    public bool CanUseNow => _flow != null && _flow.State == GameStartFlow.RunState.Playing && Time.frameCount > _flow.RunStartFrame;
    public float LastUsedTime { get; private set; } = -100f;

    public event System.Action Used;

    private GameStartFlow _flow;
    private float _readyAt;

    private void Awake()
    {
        Instance = this;
        _flow = GetComponent<GameStartFlow>();
        ChargesLeft = chargesPerRun;
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }
    private void OnEnable() => GameEvents.OnEvent += OnGameEvent;
    private void OnDisable() => GameEvents.OnEvent -= OnGameEvent;

    private void OnGameEvent(GameEventData e)
    {
        if (e.type == GameEventType.RunStarted)
        {
            ChargesLeft = chargesPerRun;
            _readyAt = 0f;
        }
    }

    private void Update()
    {
        if (!CanUseNow) return;
        for (int i = 0; i < activateKeys.Length; i++)
        {
            if (Input.GetKeyDown(activateKeys[i]))
            {
                TryActivate();
                return;
            }
        }
    }

    public bool TryActivate()
    {
        var hud = _flow != null ? _flow.hudController : null;
        if (!CanUseNow) return false;
        if (ChargesLeft <= 0)
        {
            if (hud != null) hud.ShowAlert("NO DISTRACTIONS LEFT");
            return false;
        }
        if (CooldownRemaining > 0f)
        {
            if (hud != null) hud.ShowAlert($"DISTRACTION READY IN {Mathf.CeilToInt(CooldownRemaining)}");
            return false;
        }

        ChargesLeft--;
        _readyAt = Time.time + cooldownSeconds;
        LastUsedTime = Time.time;
        if (_flow != null) _flow.DistractionsUsed++;

        var pressure = PolicePressure.Instance;
        if (pressure != null) pressure.Relieve(pressureRelief, suppressSeconds);

        ProceduralSfx.Play(ProceduralSfx.DistractionPulse, 0.8f);
        if (hud != null) hud.ShowAlert($"DISTRACTION!  Police pressure -{Mathf.RoundToInt(pressureRelief)}");
        GameEvents.Raise(GameEventType.DistractionUsed, ChargesLeft);
        Used?.Invoke();
        return true;
    }
}
