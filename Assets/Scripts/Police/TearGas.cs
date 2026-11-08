using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Police lob tear gas into Claire's lane. It lands well ahead so she has time to switch lanes;
/// running through it slows her and clouds the screen, but never costs health.
/// </summary>
public class TearGasDirector : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        GameStartFlow.RegisterBootstrap(flow =>
        {
            if (flow.GetComponent<TearGasDirector>() == null)
                flow.gameObject.AddComponent<TearGasDirector>();
        });
    }

    private const float FirstThrowDelay = 18f;
    private const float IntervalJitter = 2f;
    private const float RetryDelay = 2f;
    private const float LandAheadSeconds = 3f;  // of Claire's running time
    private const float ThrowerMinAhead = 8f;
    private const float ThrowerMaxAhead = 60f;
    private const float VanClearDistance = 45f;
    private const float SlowMultiplier = 0.7f;
    private const float SlowLingerSeconds = 1f;
    private const float HazeFadeSeconds = 2.5f;
    private const float HazeAlpha = 0.62f;

    public static TearGasDirector Instance { get; private set; }

    private PlayerLaneMovement _claire;
    private Material _gas, _canister;
    private Image _haze;
    private float _throwTimer = FirstThrowDelay;
    private float _slowUntil;
    private float _hazeLevel;

    private static float IntervalForDifficulty()
    {
        switch (GameSettings.Difficulty)
        {
            case GameSettings.DifficultyLevel.Easy: return 18f;
            case GameSettings.DifficultyLevel.Hard: return 9f;
            default: return 13f;
        }
    }

    private void Awake() => Instance = this;

    private void Start()
    {
        _claire = FindFirstObjectByType<PlayerLaneMovement>();
        var fx = Resources.Load<Material>("FxParticle");
        if (fx == null) return;

        _gas = new Material(fx);
        _gas.SetTexture("_BaseMap", SoftDot());
        _gas.SetColor("_BaseColor", new Color(0.78f, 0.86f, 0.7f, 0.5f));
        _canister = new Material(fx);
        _canister.SetColor("_BaseColor", new Color(0.2f, 0.25f, 0.2f, 1f));
        BuildHaze();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_gas != null) Destroy(_gas);
        if (_canister != null) Destroy(_canister);
    }

    private void BuildHaze()
    {
        var canvas = new GameObject("Tear Gas Haze").AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -1; // under the HUD, so the numbers stay readable
        _haze = new GameObject("Haze").AddComponent<Image>();
        _haze.transform.SetParent(canvas.transform, false);
        _haze.raycastTarget = false;
        _haze.rectTransform.anchorMin = Vector2.zero;
        _haze.rectTransform.anchorMax = Vector2.one;
        _haze.rectTransform.offsetMin = _haze.rectTransform.offsetMax = Vector2.zero;
        SetHaze(0f);
    }

    private void SetHaze(float level)
    {
        _hazeLevel = level;
        if (_haze == null) return;
        _haze.color = new Color(0.72f, 0.8f, 0.62f, level * HazeAlpha);
        _haze.enabled = level > 0f;
    }

    private void Update()
    {
        if (_claire == null) return;
        _claire.HazardSpeedMultiplier = Time.time < _slowUntil ? SlowMultiplier : 1f;
        if (_hazeLevel > 0f) SetHaze(Mathf.Max(0f, _hazeLevel - Time.deltaTime / HazeFadeSeconds));

        if (!GameStartFlow.IsGameplayActive || _gas == null) return;
        _throwTimer -= Time.deltaTime;
        if (_throwTimer > 0f) return;

        if (CanThrow())
        {
            Throw();
            _throwTimer = IntervalForDifficulty() + Random.Range(-IntervalJitter, IntervalJitter);
        }
        else
        {
            _throwTimer = RetryDelay;
        }
    }

    /// <summary>Stays out of roadblock approaches and the finish, so threats don't stack.</summary>
    private bool CanThrow()
    {
        var blocks = RoadblockDirector.Instance;
        if (blocks == null) return true;
        if (!blocks.ThreatWindowOpen) return false;
        return blocks.Van == null || blocks.Van.FinishZ - _claire.transform.position.z > VanClearDistance;
    }

    private void Throw()
    {
        Vector3 c = _claire.transform.position;
        float landX = LaneMath.SharedLaneX(LaneMath.SharedNearestLane(c.x));
        float groundY = c.y - _claire.RideHeight;
        float landZ = c.z + _claire.forwardSpeed * LandAheadSeconds;
        TearGasCanister.Launch(this, ThrowOrigin(c, landZ), new Vector3(landX, groundY, landZ), _canister);

        ProceduralSfx.Play(ProceduralSfx.Jump, 0.35f);
        var flow = GameStartFlow.Instance;
        if (flow != null && flow.hudController != null) flow.hudController.ShowAlert("TEAR GAS! CHANGE LANE");
    }

    /// <summary>From an officer ahead if there is one, otherwise dropped from above the road.</summary>
    private Vector3 ThrowOrigin(Vector3 claire, float landZ)
    {
        foreach (var officer in FindObjectsByType<PoliceOfficer>(FindObjectsSortMode.None))
        {
            float ahead = officer.transform.position.z - claire.z;
            if (officer.IsDangerous && ahead > ThrowerMinAhead && ahead < ThrowerMaxAhead)
                return officer.transform.position + Vector3.up * 1.6f;
        }
        return new Vector3(claire.x, claire.y + 14f, landZ + 14f);
    }

    public void SpawnCloud(Vector3 position) => TearGasCloud.Create(this, position, _gas);

    public PlayerLaneMovement Claire => _claire;

    public void OnClaireInGas(bool firstContact)
    {
        _slowUntil = Time.time + SlowLingerSeconds;
        SetHaze(1f);
        if (!firstContact) return;
        ProceduralSfx.Play(ProceduralSfx.Dodge, 0.8f);
        var flow = GameStartFlow.Instance;
        if (flow != null && flow.hudController != null) flow.hudController.ShowAlert("CAN'T SEE! GET OUT OF THE GAS");
    }

    private static Texture2D s_dot;

    private static Texture2D SoftDot()
    {
        if (s_dot != null) return s_dot;
        const int size = 32;
        s_dot = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), Vector2.one * (size * 0.5f)) / (size * 0.5f);
                s_dot.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.SmoothStep(1f, 0f, d)));
            }
        }
        s_dot.Apply();
        return s_dot;
    }
}

/// <summary>The canister in flight: arcs to its landing spot and bursts into a cloud.</summary>
public class TearGasCanister : MonoBehaviour
{
    private const float FlightSeconds = 1.1f;
    private const float ArcHeight = 4f;

    private TearGasDirector _director;
    private Vector3 _from, _to;
    private float _age;

    public static void Launch(TearGasDirector director, Vector3 from, Vector3 to, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "Tear Gas Canister";
        Destroy(go.GetComponent<Collider>());
        go.GetComponent<Renderer>().sharedMaterial = material;
        go.transform.position = from;
        go.transform.localScale = new Vector3(0.28f, 0.34f, 0.28f);
        var canister = go.AddComponent<TearGasCanister>();
        canister._director = director;
        canister._from = from;
        canister._to = to;
    }

    private void Update()
    {
        var flow = GameStartFlow.Instance;
        if (flow == null || _director == null || flow.State != GameStartFlow.RunState.Playing)
        {
            Destroy(gameObject);
            return;
        }

        _age += Time.deltaTime;
        float t = Mathf.Clamp01(_age / FlightSeconds);
        transform.position = Vector3.Lerp(_from, _to, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * ArcHeight);
        transform.Rotate(540f * Time.deltaTime, 200f * Time.deltaTime, 0f);
        if (t < 1f) return;

        ProceduralSfx.Play(ProceduralSfx.Dodge, 0.5f);
        _director.SpawnCloud(_to);
        Destroy(gameObject);
    }
}

/// <summary>A gas cloud filling one lane. Only that lane is affected, so there is always a way around.</summary>
public class TearGasCloud : MonoBehaviour
{
    private const float LifeSeconds = 7f;
    private const float StopEmittingBefore = 2f;
    private const float ParticleLife = 2f;
    private const float HalfLength = 3.5f;
    private const float HalfWidth = 1.2f;
    private const float EffectDelay = 0.25f;

    private TearGasDirector _director;
    private ParticleSystem _system;
    private float _age;
    private bool _touched;

    public static void Create(TearGasDirector director, Vector3 position, Material material)
    {
        var go = new GameObject("Tear Gas Cloud");
        go.SetActive(false); // modules can only be configured before the system starts
        go.transform.position = position + Vector3.up * 1.2f;

        var system = go.AddComponent<ParticleSystem>();
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
        var main = system.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = ParticleLife;
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(1.6f, 3f);
        main.maxParticles = 160;
        var emission = system.emission;
        emission.rateOverTime = 60f;
        var shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(HalfWidth * 2f, 1.6f, HalfLength * 2f);
        var fade = system.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
        fade.color = gradient;

        var cloud = go.AddComponent<TearGasCloud>();
        cloud._director = director;
        cloud._system = system;
        go.SetActive(true);
    }

    private void Update()
    {
        var flow = GameStartFlow.Instance;
        if (flow == null || _director == null || flow.State != GameStartFlow.RunState.Playing)
        {
            Destroy(gameObject);
            return;
        }

        _age += Time.deltaTime;
        if (_age >= LifeSeconds)
        {
            Destroy(gameObject);
            return;
        }

        bool emitting = _age < LifeSeconds - StopEmittingBefore;
        var emission = _system.emission;
        emission.enabled = emitting;

        var claire = _director.Claire;
        if (!emitting || _age < EffectDelay || claire == null || !GameStartFlow.IsGameplayActive) return;
        Vector3 offset = claire.transform.position - transform.position;
        if (Mathf.Abs(offset.x) > HalfWidth || Mathf.Abs(offset.z) > HalfLength) return;
        _director.OnClaireInGas(!_touched);
        _touched = true;
    }
}
