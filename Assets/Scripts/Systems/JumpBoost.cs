using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A green arrow on the road. Run over it and for a few seconds Claire's jump is high enough to clear traffic,
/// with a flip animation, a launch burst and a sparkle trail. Roadblocks still cannot be jumped.
/// </summary>
public class JumpBoost : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        GameStartFlow.RegisterBootstrap(flow =>
        {
            if (flow.GetComponent<JumpBoost>() == null)
                flow.gameObject.AddComponent<JumpBoost>();
        });
    }

    public static bool Active { get; private set; }

    private const float BoostSeconds = 8f;
    private const float FirstSpawnDelay = 8f;
    private const float SpawnAhead = 40f;
    private const float PickupHeight = 1.1f;
    private const float PickupReachX = 1.4f;
    private const float PickupReachZ = 1.1f;
    private const float SpinSpeed = 170f;
    private const float RetrySeconds = 2f;
    private const float TrailRate = 35f;
    private const float AirTrailRate = 140f;
    private const int LaunchBurst = 60;
    private static readonly Vector2 SpawnInterval = new Vector2(11f, 17f);
    private static readonly Color Lime = new Color(0.45f, 1f, 0.35f, 1f);

    private GameStartFlow _flow;
    private PlayerLaneMovement _claire;
    private Material _glow, _sparkle;
    private ParticleSystem _particles;
    private Transform _pickup;
    private float _nextSpawnAt, _until, _trailDebt;
    private GameObject _hud;
    private Image _hudFill;

    private void Start()
    {
        _flow = GetComponent<GameStartFlow>();
        _claire = FindFirstObjectByType<PlayerLaneMovement>();
        var fx = Resources.Load<Material>("FxParticle");
        if (_claire == null || fx == null) return;

        _glow = new Material(fx);
        _glow.SetColor("_BaseColor", Lime);
        _sparkle = new Material(fx);
        _sparkle.SetTexture("_BaseMap", RescueSparkles.Star());
        _sparkle.SetColor("_BaseColor", Lime);
        BuildParticles();
        BuildHud();
        _nextSpawnAt = Time.time + FirstSpawnDelay;
        _claire.BoostedJump += OnBoostedJump;
    }

    private void OnDestroy()
    {
        Active = false;
        if (_claire != null) _claire.BoostedJump -= OnBoostedJump;
        if (_glow != null) Destroy(_glow);
        if (_sparkle != null) Destroy(_sparkle);
    }

    private void BuildParticles()
    {
        var go = new GameObject("Jump Boost Sparkles");
        go.SetActive(false); // modules can only be configured before the system starts
        _particles = go.AddComponent<ParticleSystem>();
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = _sparkle;
        var main = _particles.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = false;
        main.maxParticles = 500;
        var emission = _particles.emission;
        emission.enabled = false;
        var shape = _particles.shape;
        shape.enabled = false;
        var fade = _particles.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
        fade.color = gradient;
        go.SetActive(true);
    }

    private void BuildHud()
    {
        Transform root = _flow != null && _flow.hudPanel != null ? _flow.hudPanel.transform : transform;
        var panel = RuntimeUI.CreateHudPanel(root, "JumpBoostTimer (runtime)");
        RuntimeUI.Place(panel.rectTransform, new Vector2(0f, 0.5f), new Vector2(460f, 100f), new Vector2(16f, -275f));
        SidePopup.Attach(panel.rectTransform, fromRight: false);
        var text = RuntimeUI.CreateText(panel.transform, "Text", "JUMP BOOST", 24f, TextAlignmentOptions.Center, Lime, FontStyles.Bold);
        RuntimeUI.Place(text.rectTransform, new Vector2(0.5f, 1f), new Vector2(420f, 40f), new Vector2(0f, -12f));
        RuntimeUI.FitOnOneLine(text);
        _hudFill = RuntimeUI.CreateBar(panel.transform, "Time", new Color(0f, 0f, 0f, 0.5f), Lime);
        RuntimeUI.Place((RectTransform)_hudFill.transform.parent, new Vector2(0.5f, 0f), new Vector2(400f, 30f), new Vector2(0f, 16f));
        _hud = panel.gameObject;
        _hud.SetActive(false);
    }

    private float GroundY => _claire.transform.position.y - _claire.HeightAboveGround - _claire.RideHeight;

    private void Update()
    {
        if (_claire == null || _glow == null) return;

        Active = Time.time < _until;
        _claire.JumpBoosted = Active;
        _hud.SetActive(Active);
        if (Active) _hudFill.fillAmount = (_until - Time.time) / BoostSeconds;

        if (!GameStartFlow.IsGameplayActive) return;
        if (Active) EmitTrail();
        if (_pickup != null) UpdatePickup();
        else if (!Active && _flow.State == GameStartFlow.RunState.Playing && Time.time >= _nextSpawnAt) TrySpawn();
    }

    private void TrySpawn()
    {
        var blocks = RoadblockDirector.Instance;
        float z = _claire.transform.position.z + SpawnAhead;
        int lane = blocks == null || blocks.ThreatWindowOpen ? SkateboardRide.PickClearLane(z) : -1;
        if (lane < 0)
        {
            _nextSpawnAt = Time.time + RetrySeconds;
            return;
        }
        _pickup = BuildArrow(new Vector3(LaneMath.SharedLaneX(lane), GroundY + PickupHeight, z));
    }

    private Transform BuildArrow(Vector3 position)
    {
        var root = new GameObject("Jump Boost Pickup").transform;
        root.position = position;
        ArrowPart(root, new Vector3(0f, -0.25f, 0f), new Vector3(0.26f, 0.8f, 0.26f), 0f);
        ArrowPart(root, new Vector3(-0.21f, 0.27f, 0f), new Vector3(0.26f, 0.72f, 0.26f), -45f);
        ArrowPart(root, new Vector3(0.21f, 0.27f, 0f), new Vector3(0.26f, 0.72f, 0.26f), 45f);
        return root;
    }

    private void ArrowPart(Transform root, Vector3 position, Vector3 size, float tilt)
    {
        var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(part.GetComponent<Collider>());
        part.GetComponent<Renderer>().sharedMaterial = _glow;
        part.transform.SetParent(root, false);
        part.transform.localPosition = position;
        part.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);
        part.transform.localScale = size;
    }

    private void UpdatePickup()
    {
        _pickup.Rotate(Vector3.up, SpinSpeed * Time.deltaTime, Space.World);
        Vector3 p = _pickup.position;
        Vector3 c = _claire.transform.position;

        if (Random.value < Time.deltaTime * 25f)
            Emit(p + Random.insideUnitSphere * 0.6f, Vector3.up * Random.Range(0.8f, 2f), 0.6f, Random.Range(0.1f, 0.22f));

        if (p.z < c.z - 3f)
        {
            Destroy(_pickup.gameObject);
            _pickup = null;
            _nextSpawnAt = Time.time + Random.Range(SpawnInterval.x, SpawnInterval.y);
            return;
        }
        if (Mathf.Abs(p.z - c.z) <= PickupReachZ && Mathf.Abs(p.x - c.x) <= PickupReachX) Collect(p);
    }

    private void Collect(Vector3 position)
    {
        Destroy(_pickup.gameObject);
        _pickup = null;
        _until = Time.time + BoostSeconds;
        _nextSpawnAt = _until + Random.Range(SpawnInterval.x, SpawnInterval.y);
        Burst(position, 45, 5f);
        ProceduralSfx.Play(ProceduralSfx.Rescue, 0.7f);
        if (_flow.hudController != null) _flow.hudController.ShowAlert("JUMP BOOST! JUMP OVER CARS");
    }

    private void OnBoostedJump()
    {
        Burst(new Vector3(_claire.transform.position.x, GroundY + 0.15f, _claire.transform.position.z), LaunchBurst, 7f);
    }

    private void Burst(Vector3 position, int count, float speed)
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 flat = Random.insideUnitCircle.normalized * Random.Range(speed * 0.5f, speed);
            Emit(position, new Vector3(flat.x, Random.Range(0.3f, 2.5f), flat.y), Random.Range(0.35f, 0.6f), Random.Range(0.14f, 0.32f));
        }
    }

    private void EmitTrail()
    {
        _trailDebt += (_claire.HeightAboveGround > 0f ? AirTrailRate : TrailRate) * Time.deltaTime;
        Vector3 feet = _claire.transform.position + Vector3.up * 0.15f;
        for (; _trailDebt >= 1f; _trailDebt -= 1f)
            Emit(feet + Random.insideUnitSphere * 0.3f, Vector3.up * Random.Range(0.1f, 0.8f), Random.Range(0.3f, 0.6f), Random.Range(0.08f, 0.2f));
    }

    private void Emit(Vector3 position, Vector3 velocity, float lifetime, float size)
    {
        var p = new ParticleSystem.EmitParams { position = position, velocity = velocity, startLifetime = lifetime, startSize = size, startColor = Color.white };
        _particles.Emit(p, 1);
    }
}
