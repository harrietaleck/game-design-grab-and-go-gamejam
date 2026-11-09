using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Speed shoes. A shoe appears on the road from time to time; run over it and Claire wears the equipped pair for a
/// short speed boost, with AJ keeping pace. Better pairs from the shop last longer. The shoes are stand-ins built
/// from simple shapes until a real model is added: only BuildShoe would need to change.
/// </summary>
public class SpeedShoes : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        GameStartFlow.RegisterBootstrap(flow =>
        {
            if (flow.GetComponent<SpeedShoes>() == null)
                flow.gameObject.AddComponent<SpeedShoes>();
        });
    }

    public struct Pair
    {
        public string name;
        public Color body, trim;
        public float seconds;
        public int price;
    }

    public static readonly Pair[] Pairs =
    {
        new Pair { name = "STREET SHOES", body = new Color(0.92f, 0.92f, 0.95f), trim = new Color(0.9f, 0.15f, 0.2f), seconds = 6f, price = 0 },
        new Pair { name = "NEON SHOES", body = new Color(0.25f, 1f, 0.45f), trim = new Color(0.05f, 0.1f, 0.1f), seconds = 7.5f, price = 150 },
        new Pair { name = "ROCKET SHOES", body = new Color(1f, 0.5f, 0.1f), trim = new Color(1f, 0.9f, 0.3f), seconds = 9f, price = 300 },
        new Pair { name = "PHANTOM SHOES", body = new Color(0.3f, 0.45f, 1f), trim = new Color(0.8f, 0.95f, 1f), seconds = 11f, price = 500 },
    };

    public static Pair Equipped => Pairs[Mathf.Clamp(ShopStore.EquippedShoes, 0, Pairs.Length - 1)];

    private const float SpeedMultiplier = 1.4f;
    private const float SpawnAhead = 58f;
    private const float FirstSpawnDelay = 20f;
    private const float RetrySeconds = 2f;
    private const float PickupHeight = 0.9f;
    private const float PickupScale = 2.2f;
    private const float ReachX = 1.4f;
    private const float ReachZ = 1.1f;
    private const float TrailRate = 70f;
    private static readonly Vector2 SpawnInterval = new Vector2(16f, 24f);

    private GameStartFlow _flow;
    private PlayerLaneMovement _claire;
    private AIThiefLaneDodge _aj;
    private Transform _pickup;
    private float _nextSpawnAt, _until, _length = 1f, _trailDebt;
    private ParticleSystem _particles;
    private Material _sparkle;
    private GameObject _hud;
    private Image _hudFill;
    private Transform _leftFoot, _leftToe, _rightFoot, _rightToe, _leftShoe, _rightShoe;

    private bool Running => Time.time < _until;

    private void Start()
    {
        _flow = GetComponent<GameStartFlow>();
        _claire = FindFirstObjectByType<PlayerLaneMovement>();
        _aj = FindFirstObjectByType<AIThiefLaneDodge>();
        var fx = Resources.Load<Material>("FxParticle");
        if (_claire == null || fx == null) return;

        foreach (var t in _claire.GetComponentsInChildren<Transform>(true))
        {
            if (t.name.EndsWith("LeftFoot")) _leftFoot = t;
            else if (t.name.EndsWith("LeftToeBase")) _leftToe = t;
            else if (t.name.EndsWith("RightFoot")) _rightFoot = t;
            else if (t.name.EndsWith("RightToeBase")) _rightToe = t;
        }
        _sparkle = new Material(fx);
        _sparkle.SetTexture("_BaseMap", RescueSparkles.Star());
        _sparkle.SetColor("_BaseColor", Equipped.body);
        BuildParticles();
        BuildHud();
        _nextSpawnAt = Time.time + FirstSpawnDelay;
    }

    private void OnDestroy()
    {
        if (_sparkle != null) Destroy(_sparkle);
    }

    // ---------------- The shoe itself ----------------

    /// <summary>One shoe, toe pointing along +Z, roughly life size. Replace this body to use a real model.</summary>
    public static GameObject BuildShoe(Pair pair)
    {
        var fx = Resources.Load<Material>("FxParticle");
        var body = new Material(fx);
        body.SetColor("_BaseColor", pair.body);
        var trim = new Material(fx);
        trim.SetColor("_BaseColor", pair.trim);

        var root = new GameObject(pair.name);
        Part(root.transform, PrimitiveType.Cube, trim, new Vector3(0f, 0.02f, 0.02f), new Vector3(0.13f, 0.04f, 0.32f));
        Part(root.transform, PrimitiveType.Cube, body, new Vector3(0f, 0.09f, -0.05f), new Vector3(0.12f, 0.11f, 0.18f));
        Part(root.transform, PrimitiveType.Sphere, body, new Vector3(0f, 0.07f, 0.1f), new Vector3(0.12f, 0.09f, 0.16f));
        Part(root.transform, PrimitiveType.Cube, trim, new Vector3(0f, 0.16f, -0.08f), new Vector3(0.125f, 0.03f, 0.13f));
        return root;
    }

    private static void Part(Transform parent, PrimitiveType type, Material material, Vector3 position, Vector3 size)
    {
        var go = GameObject.CreatePrimitive(type);
        Destroy(go.GetComponent<Collider>());
        go.GetComponent<Renderer>().sharedMaterial = material;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = size;
    }

    private static GameObject[] s_previews;

    /// <summary>An inactive template for the shop's spinning preview. Kept for the life of the game.</summary>
    public static GameObject PreviewTemplate(int index)
    {
        if (s_previews == null) s_previews = new GameObject[Pairs.Length];
        if (s_previews[index] == null)
        {
            s_previews[index] = BuildShoe(Pairs[index]);
            s_previews[index].SetActive(false);
            DontDestroyOnLoad(s_previews[index]);
        }
        return s_previews[index];
    }

    // ---------------- Effects and HUD ----------------

    private void BuildParticles()
    {
        var go = new GameObject("Speed Shoe Sparkles");
        go.SetActive(false); // modules can only be configured before the system starts
        _particles = go.AddComponent<ParticleSystem>();
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = _sparkle;
        var main = _particles.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = false;
        main.maxParticles = 300;
        var emission = _particles.emission;
        emission.enabled = false;
        var shape = _particles.shape;
        shape.enabled = false;
        var fade = _particles.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        fade.color = gradient;
        go.SetActive(true);
    }

    private void BuildHud()
    {
        Transform root = _flow != null && _flow.hudPanel != null ? _flow.hudPanel.transform : transform;
        var panel = RuntimeUI.CreateHudPanel(root, "SpeedShoesTimer (runtime)");
        // Shares the skateboard's slot: the two boosts never run together.
        RuntimeUI.Place(panel.rectTransform, new Vector2(0f, 0.5f), new Vector2(460f, 100f), new Vector2(16f, -160f));
        SidePopup.Attach(panel.rectTransform, fromRight: false);
        var text = RuntimeUI.CreateText(panel.transform, "Text", "SPEED SHOES", 24f, TextAlignmentOptions.Center, RuntimeUI.Gold, FontStyles.Bold);
        RuntimeUI.Place(text.rectTransform, new Vector2(0.5f, 1f), new Vector2(420f, 40f), new Vector2(0f, -12f));
        RuntimeUI.FitOnOneLine(text);
        _hudFill = RuntimeUI.CreateBar(panel.transform, "Time", new Color(0f, 0f, 0f, 0.5f), RuntimeUI.Gold);
        RuntimeUI.Place((RectTransform)_hudFill.transform.parent, new Vector2(0.5f, 0f), new Vector2(400f, 30f), new Vector2(0f, 16f));
        _hud = panel.gameObject;
        _hud.SetActive(false);
    }

    // ---------------- Gameplay ----------------

    private float GroundY => _claire.transform.position.y - _claire.HeightAboveGround - _claire.RideHeight;

    private void Update()
    {
        if (_claire == null || _hud == null) return;

        bool riding = SkateboardRide.Instance != null && SkateboardRide.Instance.IsRiding;
        bool boosting = Running && !riding; // the skateboard is faster, so it takes over rather than stacking
        _claire.ShoeSpeedMultiplier = boosting ? SpeedMultiplier : 1f;
        if (_aj != null) _aj.ShoeSpeedMultiplier = boosting ? SpeedMultiplier : 1f;
        _hud.SetActive(boosting);
        if (boosting) _hudFill.fillAmount = (_until - Time.time) / _length;
        if (!Running && _leftShoe != null) RemoveWornShoes();

        if (!GameStartFlow.IsGameplayActive) return;
        if (boosting) EmitTrail();
        if (_pickup != null) UpdatePickup();
        else if (!Running && !riding && _flow.State == GameStartFlow.RunState.Playing && Time.time >= _nextSpawnAt) TrySpawn();
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
        var shoe = BuildShoe(Equipped);
        shoe.name = "Speed Shoe Pickup";
        shoe.transform.position = new Vector3(LaneMath.SharedLaneX(lane), GroundY + PickupHeight, z);
        shoe.transform.localScale = Vector3.one * PickupScale;
        _pickup = shoe.transform;
    }

    private void UpdatePickup()
    {
        _pickup.Rotate(Vector3.up, 170f * Time.deltaTime, Space.World);
        Vector3 p = _pickup.position;
        Vector3 c = _claire.transform.position;
        if (Mathf.Abs(p.z - c.z) <= ReachZ && Mathf.Abs(p.x - c.x) <= ReachX)
        {
            Destroy(_pickup.gameObject);
            _pickup = null;
            Activate();
        }
        else if (p.z < c.z - 4f)
        {
            Destroy(_pickup.gameObject);
            _pickup = null;
            _nextSpawnAt = Time.time + Random.Range(SpawnInterval.x, SpawnInterval.y);
        }
    }

    private void Activate()
    {
        var pair = Equipped;
        _length = pair.seconds;
        _until = Time.time + _length;
        _nextSpawnAt = _until + Random.Range(SpawnInterval.x, SpawnInterval.y);
        WearShoes(pair);
        ProceduralSfx.Play(ProceduralSfx.Rescue, 0.7f);
        if (_flow.hudController != null) _flow.hudController.ShowAlert("SPEED SHOES!");
    }

    private void WearShoes(Pair pair)
    {
        RemoveWornShoes();
        if (_leftFoot == null || _leftToe == null || _rightFoot == null || _rightToe == null) return;
        _leftShoe = BuildShoe(pair).transform;
        _rightShoe = BuildShoe(pair).transform;
    }

    private void RemoveWornShoes()
    {
        if (_leftShoe != null) Destroy(_leftShoe.gameObject);
        if (_rightShoe != null) Destroy(_rightShoe.gameObject);
        _leftShoe = _rightShoe = null;
    }

    private void LateUpdate()
    {
        if (_leftShoe == null) return;
        FitToFoot(_leftShoe, _leftFoot, _leftToe);
        FitToFoot(_rightShoe, _rightFoot, _rightToe);
    }

    /// <summary>Follows the animated foot from its bone positions, so it works whatever way the bones are oriented.</summary>
    private static void FitToFoot(Transform shoe, Transform foot, Transform toe)
    {
        Vector3 along = toe.position - foot.position;
        if (along.sqrMagnitude < 0.0001f) return;
        shoe.SetPositionAndRotation(Vector3.Lerp(foot.position, toe.position, 0.6f) + Vector3.down * 0.07f,
                                    Quaternion.LookRotation(along, Vector3.up));
    }

    private void EmitTrail()
    {
        _trailDebt += TrailRate * Time.deltaTime;
        Vector3 feet = new Vector3(_claire.transform.position.x, GroundY + 0.15f, _claire.transform.position.z - 0.2f);
        for (; _trailDebt >= 1f; _trailDebt -= 1f)
        {
            var p = new ParticleSystem.EmitParams
            {
                position = feet + Random.insideUnitSphere * 0.25f,
                velocity = Vector3.up * Random.Range(0.1f, 0.7f),
                startLifetime = Random.Range(0.25f, 0.5f),
                startSize = Random.Range(0.08f, 0.2f),
                startColor = Color.white
            };
            _particles.Emit(p, 1);
        }
    }
}
