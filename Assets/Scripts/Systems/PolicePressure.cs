using UnityEngine;

/// <summary>
/// Police pressure (Task 2.4): a 0-100 meter driven only by real gameplay events.
/// Rises when Claire crashes, AJ has a near miss or a gold stack is grabbed; cools down when things are calm.
/// At the maximum the police grab AJ (he becomes caught and the rescue window opens).
/// Unity owns this value; the HUD and dialogue only read it.
/// </summary>
public class PolicePressure : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        GameStartFlow.RegisterBootstrap(flow =>
        {
            if (flow.GetComponent<PolicePressure>() == null)
                flow.gameObject.AddComponent<PolicePressure>();
        });
    }

    public static PolicePressure Instance { get; private set; }

    [Header("Range")]
    public float maxPressure = 100f;
    [Tooltip("Pressure at the start of a run.")]
    public float startPressure = 10f;

    [Header("Gains (per event)")]
    public float claireCollisionGain = 18f;
    public float ajNearMissGain = 6f;
    public float goldStackGain = 3f;

    [Header("Cooling")]
    [Tooltip("Seconds without a new pressure event before the meter starts cooling.")]
    public float calmDelay = 3f;
    [Tooltip("Pressure lost per second while calm.")]
    public float decayPerSecond = 2.5f;

    [Header("Thresholds")]
    [Range(0f, 1f)] public float highThreshold = 0.7f;
    [Range(0f, 1f)] public float rearmThreshold = 0.5f;
    [Tooltip("When pressure hits the maximum, the police grab AJ.")]
    public bool captureAtMax = true;
    [Tooltip("Pressure after a successful rescue.")]
    public float afterRescuePressure = 45f;

    public float Value { get; private set; }
    public float Normalized => maxPressure <= 0f ? 0f : Value / maxPressure;
    public bool IsHigh => Normalized >= highThreshold;
    public bool GainSuppressed => Time.time < _suppressGainUntil;

    public event System.Action<float> Changed; // normalized

    private GameStartFlow _flow;
    private AIThiefLaneDodge _thief;
    private float _lastEventTime;
    private float _suppressGainUntil;
    private bool _highArmed = true;

    private void Awake()
    {
        Instance = this;
        _flow = GetComponent<GameStartFlow>();
        if (_flow != null) _flow.PolicePressureProvider = () => Value;
        Value = startPressure;
    }

    private void Start()
    {
        _thief = FindFirstObjectByType<AIThiefLaneDodge>();
    }

    private void OnEnable() => GameEvents.OnEvent += OnGameEvent;
    private void OnDisable() => GameEvents.OnEvent -= OnGameEvent;

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_flow != null) _flow.PolicePressureProvider = null;
    }

    private void Update()
    {
        if (_flow == null || _flow.State != GameStartFlow.RunState.Playing)
            return;
        if (Time.time - _lastEventTime >= calmDelay && Value > 0f)
            Set(Value - decayPerSecond * Time.deltaTime);
    }

    private void OnGameEvent(GameEventData e)
    {
        switch (e.type)
        {
            case GameEventType.RunStarted:
                _highArmed = true;
                _suppressGainUntil = 0f;
                Set(startPressure);
                break;
            case GameEventType.ClaireCollision: Add(claireCollisionGain); break;
            case GameEventType.AJNearMiss: Add(ajNearMissGain); break;
            case GameEventType.GoldStackCollected: Add(goldStackGain); break;
            case GameEventType.AJRescued: Set(afterRescuePressure); _highArmed = true; break;
        }
    }

    /// <summary>Adds pressure from a real gameplay event (ignored while a distraction is active or outside Playing).</summary>
    public void Add(float amount)
    {
        if (_flow == null || _flow.State != GameStartFlow.RunState.Playing)
            return;
        _lastEventTime = Time.time;
        if (amount > 0f && GainSuppressed)
            return;
        Set(Value + amount);
    }

    /// <summary>Distraction Pulse: lowers pressure and blocks new gains for a few seconds.</summary>
    public void Relieve(float amount, float suppressSeconds)
    {
        _suppressGainUntil = Time.time + Mathf.Max(0f, suppressSeconds);
        Set(Value - Mathf.Abs(amount));
    }

    private void Set(float v)
    {
        float old = Value;
        Value = Mathf.Clamp(v, 0f, maxPressure);
        if (!Mathf.Approximately(old, Value))
            Changed?.Invoke(Normalized);

        if (_highArmed && IsHigh)
        {
            _highArmed = false;
            GameEvents.Raise(GameEventType.PolicePressureHigh, Mathf.RoundToInt(Value));
            ProceduralSfx.Play(ProceduralSfx.Alarm, 0.6f);
        }
        else if (!_highArmed && Normalized <= rearmThreshold)
        {
            _highArmed = true;
        }

        if (captureAtMax && Value >= maxPressure && _flow != null && _flow.State == GameStartFlow.RunState.Playing)
        {
            if (_thief == null) _thief = FindFirstObjectByType<AIThiefLaneDodge>();
            if (_thief != null && !_thief.IsCaught)
                _thief.SetCaught("police");
        }
    }
}
