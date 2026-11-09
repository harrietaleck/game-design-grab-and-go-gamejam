using TMPro;
using UnityEngine;

/// <summary>
/// A bulletproof jacket that stops a few bullets. Picked up on the road, or worn from the start of every run once
/// bought in the shop. It is a stand-in built from simple shapes until a real model is added: only BuildJacket would change.
/// </summary>
public class BulletJacket : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        GameStartFlow.RegisterBootstrap(flow =>
        {
            if (flow.GetComponent<BulletJacket>() == null)
                flow.gameObject.AddComponent<BulletJacket>();
        });
    }

    public static BulletJacket Instance { get; private set; }

    private const int MaxBlocks = 3;
    private const float FirstSpawnDelay = 25f;
    private const float SpawnAhead = 50f;
    private const float RetrySeconds = 2f;
    private const float PickupHeight = 1.1f;
    private const float PickupScale = 1.8f;
    private const float ReachX = 1.4f;
    private const float ReachZ = 1.1f;
    private static readonly Vector2 SpawnInterval = new Vector2(22f, 32f);
    private static readonly Color Body = new Color(0.1f, 0.14f, 0.28f);
    private static readonly Color Strap = new Color(1f, 0.82f, 0.25f);

    private GameStartFlow _flow;
    private PlayerLaneMovement _claire;
    private Transform _spine, _worn, _pickup;
    private Material _spark;
    private int _blocks;
    private float _nextSpawnAt;
    private GameObject _hud;
    private TextMeshProUGUI _hudText;

    private void Awake() => Instance = this;

    private void Start()
    {
        _flow = GetComponent<GameStartFlow>();
        _claire = FindFirstObjectByType<PlayerLaneMovement>();
        var fx = Resources.Load<Material>("FxParticle");
        if (_claire == null || fx == null) return;

        foreach (var t in _claire.GetComponentsInChildren<Transform>(true))
            if (t.name.EndsWith("Spine1")) _spine = t;
        _spark = new Material(fx);
        _spark.SetColor("_BaseColor", Strap);
        BuildHud();
        _nextSpawnAt = Time.time + FirstSpawnDelay;
        if (ShopStore.Owns(ShopStore.Jacket)) Equip();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_spark != null) Destroy(_spark);
    }

    /// <summary>A sleeveless vest centred on the chest, front along +Z.</summary>
    public static GameObject BuildJacket()
    {
        var fx = Resources.Load<Material>("FxParticle");
        var body = new Material(fx);
        body.SetColor("_BaseColor", Body);
        var strap = new Material(fx);
        strap.SetColor("_BaseColor", Strap);

        var root = new GameObject("Bulletproof Jacket");
        Part(root.transform, body, Vector3.zero, new Vector3(0.36f, 0.44f, 0.23f));
        Part(root.transform, strap, new Vector3(0f, 0.07f, 0.118f), new Vector3(0.3f, 0.05f, 0.01f));
        Part(root.transform, strap, new Vector3(0f, -0.09f, 0.118f), new Vector3(0.3f, 0.05f, 0.01f));
        return root;
    }

    private static void Part(Transform parent, Material material, Vector3 position, Vector3 size)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(go.GetComponent<Collider>());
        go.GetComponent<Renderer>().sharedMaterial = material;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = size;
    }

    private void BuildHud()
    {
        Transform root = _flow != null && _flow.hudPanel != null ? _flow.hudPanel.transform : transform;
        var panel = RuntimeUI.CreateHudPanel(root, "JacketStatus (runtime)");
        RuntimeUI.Place(panel.rectTransform, new Vector2(1f, 0f), new Vector2(420f, 90f), new Vector2(-16f, 210f));
        _hudText = RuntimeUI.CreateText(panel.transform, "Text", "", 24f, TextAlignmentOptions.Center, RuntimeUI.Gold, FontStyles.Bold);
        RuntimeUI.Stretch(_hudText.rectTransform, 12f);
        RuntimeUI.FitOnOneLine(_hudText);
        _hud = panel.gameObject;
        _hud.SetActive(false);
    }

    private void RefreshHud()
    {
        _hud.SetActive(_blocks > 0);
        _hudText.text = $"JACKET  {new string('●', _blocks)}{new string('○', MaxBlocks - _blocks)}";
    }

    private void Equip()
    {
        _blocks = MaxBlocks;
        if (_worn == null && _spine != null) _worn = BuildJacket().transform;
        RefreshHud();
    }

    /// <summary>Stops a bullet if the jacket has any strength left. Returns true if it did.</summary>
    public bool TryBlockBullet()
    {
        if (_blocks <= 0 || _claire == null) return false;
        _blocks--;
        RefreshHud();
        ImpactSparks.Burst(_claire.transform.position + Vector3.up * 1.2f + Vector3.forward * 0.2f, _spark, 12);
        ProceduralSfx.Play(ProceduralSfx.BulletHit, 0.9f);
        if (_flow.hudController != null) _flow.hudController.ShowAlert(_blocks > 0 ? "JACKET STOPPED IT!" : "JACKET DESTROYED!");
        if (_blocks == 0 && _worn != null)
        {
            Destroy(_worn.gameObject);
            _worn = null;
        }
        return true;
    }

    private float GroundY => _claire.transform.position.y - _claire.HeightAboveGround - _claire.RideHeight;

    private void Update()
    {
        if (_claire == null || _hud == null || !GameStartFlow.IsGameplayActive) return;
        if (_pickup != null) UpdatePickup();
        else if (_blocks < MaxBlocks && _flow.State == GameStartFlow.RunState.Playing && Time.time >= _nextSpawnAt) TrySpawn();
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
        var jacket = BuildJacket();
        jacket.name = "Bulletproof Jacket Pickup";
        jacket.transform.position = new Vector3(LaneMath.SharedLaneX(lane), GroundY + PickupHeight, z);
        jacket.transform.localScale = Vector3.one * PickupScale;
        _pickup = jacket.transform;
    }

    private void UpdatePickup()
    {
        _pickup.Rotate(Vector3.up, 150f * Time.deltaTime, Space.World);
        Vector3 p = _pickup.position;
        Vector3 c = _claire.transform.position;
        bool reached = Mathf.Abs(p.z - c.z) <= ReachZ && Mathf.Abs(p.x - c.x) <= ReachX;
        if (!reached && p.z >= c.z - 4f) return;

        Destroy(_pickup.gameObject);
        _pickup = null;
        _nextSpawnAt = Time.time + Random.Range(SpawnInterval.x, SpawnInterval.y);
        if (!reached) return;
        Equip();
        ProceduralSfx.Play(ProceduralSfx.Rescue, 0.7f);
        if (_flow.hudController != null) _flow.hudController.ShowAlert("BULLETPROOF JACKET!");
    }

    private void LateUpdate()
    {
        if (_worn == null || _spine == null) return;
        _worn.SetPositionAndRotation(_spine.position, Quaternion.Euler(_claire.transform.eulerAngles.x, 0f, 0f));
    }
}
