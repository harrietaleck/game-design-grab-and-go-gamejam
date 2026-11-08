using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Police roadblocks: patrol cars parked across three of the four lanes, leaving one open gap.
/// They replace a normal traffic row (so spacing and reachability rules still apply), use LaneObstacle triggers
/// (so Claire's damage, AJ's dodging, the rescue and officer fairness all work unchanged), and are announced
/// on the HUD with the open lane well before Claire reaches them.
/// </summary>
public class RoadblockDirector : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        GameStartFlow.RegisterBootstrap(flow =>
        {
            if (flow.GetComponent<RoadblockDirector>() == null)
                flow.gameObject.AddComponent<RoadblockDirector>();
        });
    }

    [Tooltip("World Z of the earliest roadblock.")]
    public float firstRoadblockZ = 130f;
    [Tooltip("Distance between roadblocks at the start and at the end of a run.")]
    public Vector2 spacingRange = new Vector2(150f, 90f);
    public float spacingJitter = 20f;
    [Tooltip("The HUD warning appears when Claire is this far away.")]
    public float warningDistance = 48f;
    [Tooltip("Health lost when Claire hits a roadblock (traffic costs 10).")]
    public float healthDamage = 20f;
    public float carScale = 0.95f;

    private class Entry
    {
        public Transform root;
        public float z;
        public int gap;
    }

    private readonly List<Entry> _active = new();
    private GameStartFlow _flow;
    private RoadSpawner _spawner;
    private PlayerLaneMovement _claire;
    private PoliceSet _set;
    private float _nextZ;
    private Material _lightMaterial;
    private Entry _warned;

    private GameObject _warning;
    private TextMeshProUGUI _warningText;
    private Image[] _laneMarks;

    private void Awake()
    {
        _flow = GetComponent<GameStartFlow>();
        _nextZ = firstRoadblockZ;
        BuildWarningUI();
    }

    private void Start()
    {
        _set = Resources.Load<PoliceSet>("PoliceSet");
        _spawner = FindFirstObjectByType<RoadSpawner>();
        _claire = FindFirstObjectByType<PlayerLaneMovement>();
        if (_set != null && _set.policeCar != null && _spawner != null && _claire != null)
            _spawner.RowOverride = TryPlaceRoadblock;
    }

    private void OnDestroy()
    {
        if (_spawner != null) _spawner.RowOverride = null;
        if (_lightMaterial != null) Destroy(_lightMaterial);
    }

    // ---------------- Placement ----------------

    private bool[] TryPlaceRoadblock(float z, Transform tile, bool[] previousFree)
    {
        if (z < _nextZ) return null;

        int laneCount = LaneMath.SharedLaneCount;
        int gap = PickGap(laneCount, previousFree);
        if (gap < 0) return null;

        float secondsAway = Mathf.Max(0f, z - _claire.transform.position.z) / Mathf.Max(0.1f, _claire.forwardSpeed);
        float progress = _flow != null ? Mathf.Clamp01((_flow.TimeSurvived + secondsAway) / _flow.RunDuration) : 0f;
        _nextZ = z + Mathf.Lerp(spacingRange.x, spacingRange.y, progress) + Random.Range(-spacingJitter, spacingJitter);

        var root = new GameObject("Police Roadblock").transform;
        root.SetParent(tile, false);
        root.position = new Vector3(0f, _spawner.obstacleY, z);

        // Blocked lanes on each side of the gap: pairs get one car parked across both, a single lane gets one car.
        for (int lane = 0; lane < laneCount; lane++)
        {
            if (lane == gap) continue;
            bool pair = lane + 1 < laneCount && lane + 1 != gap;
            AddCar(root, lane, pair ? lane + 1 : lane, z);
            if (pair) lane++;
        }

        _active.Add(new Entry { root = root, z = z, gap = gap });

        var free = new bool[laneCount];
        free[gap] = true;
        return free;
    }

    /// <summary>A random lane that can be reached from the previous row's free lanes, or -1.</summary>
    private int PickGap(int laneCount, bool[] previousFree)
    {
        var options = new List<int>();
        for (int lane = 0; lane < laneCount; lane++)
        {
            if (previousFree == null || previousFree.Length != laneCount) { options.Add(lane); continue; }
            for (int prev = 0; prev < laneCount; prev++)
            {
                if (previousFree[prev] && Mathf.Abs(prev - lane) <= _spawner.maxLaneShiftBetweenRows)
                {
                    options.Add(lane);
                    break;
                }
            }
        }
        return options.Count > 0 ? options[Random.Range(0, options.Count)] : -1;
    }

    private void AddCar(Transform root, int firstLane, int lastLane, float z)
    {
        bool across = firstLane != lastLane;
        float x = (LaneMath.SharedLaneX(firstLane) + LaneMath.SharedLaneX(lastLane)) * 0.5f;
        float yaw = across ? (Random.value < 0.5f ? 90f : -90f) : 180f + Random.Range(-8f, 8f);

        var car = Instantiate(_set.policeCar, new Vector3(x, root.position.y, z), Quaternion.Euler(0f, yaw, 0f), root);
        car.name = "Police Car";
        car.transform.localScale *= carScale;
        foreach (var c in car.GetComponentsInChildren<Collider>(true)) Destroy(c);
        foreach (var a in car.GetComponentsInChildren<Animator>(true)) Destroy(a);

        Bounds bounds = WorldBounds(car);
        car.transform.position += Vector3.up * (root.position.y - bounds.min.y); // sit on the road
        AddRoofLights(car, bounds.size.y, bounds.center.x, bounds.center.z, root.position.y);

        float depth = across ? 2.6f : 5.4f;
        for (int lane = firstLane; lane <= lastLane; lane++)
            AddLaneBlocker(root, car, lane, z, depth);
    }

    private void AddLaneBlocker(Transform root, GameObject car, int lane, float z, float depth)
    {
        var go = new GameObject("Roadblock Lane");
        go.transform.SetParent(root, false);
        go.transform.position = new Vector3(LaneMath.SharedLaneX(lane), root.position.y + 1f, z);

        var obstacle = go.AddComponent<LaneObstacle>();
        obstacle.healthDamage = healthDamage;
        LaneObstacleSpawner.EnsureObstacleSetup(go);
        go.GetComponent<BoxCollider>().size = new Vector3(LaneMath.SharedLaneDistance * 0.75f, 2.2f, depth);
        go.AddComponent<RoadblockLane>().car = car;
    }

    private void AddRoofLights(GameObject car, float carHeight, float x, float z, float groundY)
    {
        var body = car.GetComponentInChildren<Renderer>(true);
        if (body == null) return;
        if (_lightMaterial == null)
        {
            _lightMaterial = new Material(body.sharedMaterial) { name = "Roadblock Light" };
            _lightMaterial.SetTexture("_BaseMap", null);
        }

        foreach (float side in new[] { -0.35f, 0.35f })
        {
            var light = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            light.name = "Police Light";
            Destroy(light.GetComponent<Collider>());
            light.GetComponent<Renderer>().sharedMaterial = _lightMaterial;
            light.transform.SetParent(car.transform, true);
            light.transform.position = new Vector3(x, groundY + carHeight + 0.1f, z) + car.transform.right * side;
            light.transform.localScale = Vector3.one * 0.3f / Mathf.Max(0.01f, car.transform.lossyScale.x);
            light.AddComponent<PoliceLightBlink>();
        }
    }

    private static Bounds WorldBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        return b;
    }

    // ---------------- Warning HUD ----------------

    private void BuildWarningUI()
    {
        Transform root = _flow != null && _flow.hudPanel != null ? _flow.hudPanel.transform : transform;
        var panel = RuntimeUI.CreateWindow(root, "RoadblockWarning (runtime)", new Color(1f, 0.7f, 0.35f, 1f), 0.7f);
        panel.raycastTarget = false;
        RuntimeUI.Place(panel.rectTransform, new Vector2(0.5f, 0f), new Vector2(800f, 120f), new Vector2(0f, 360f));
        _warningText = RuntimeUI.CreateText(panel.transform, "Text", "ROADBLOCK AHEAD", 26f, TextAlignmentOptions.Center, RuntimeUI.Gold, FontStyles.Bold);
        RuntimeUI.Place(_warningText.rectTransform, new Vector2(0.5f, 1f), new Vector2(770f, 56f), new Vector2(0f, -8f));
        _laneMarks = new Image[LaneMath.SharedLaneCount];
        for (int i = 0; i < _laneMarks.Length; i++)
        {
            var mark = RuntimeUI.CreatePanel(panel.transform, $"Lane{i}", Color.white);
            mark.raycastTarget = false;
            float x = (i - (_laneMarks.Length - 1) * 0.5f) * 130f;
            RuntimeUI.Place(mark.rectTransform, new Vector2(0.5f, 0f), new Vector2(110f, 30f), new Vector2(x, 14f));
            _laneMarks[i] = mark;
        }
        _warning = panel.gameObject;
        _warning.SetActive(false);
    }

    private void Update()
    {
        if (_warning == null) return;
        Entry next = null;
        if (_claire != null && _flow != null && _flow.State == GameStartFlow.RunState.Playing)
        {
            float cz = _claire.transform.position.z;
            _active.RemoveAll(e => e.root == null || e.z < cz - 2f);
            foreach (var e in _active)
            {
                float dz = e.z - cz;
                if (dz > 0f && dz <= warningDistance && (next == null || e.z < next.z)) next = e;
            }
        }

        _warning.SetActive(next != null);
        if (next == null) return;

        if (next != _warned)
        {
            _warned = next;
            ProceduralSfx.Play(ProceduralSfx.Alarm, 0.45f);
        }

        bool inGap = _claire.CurrentLane == next.gap;
        _warningText.text = inGap ? "ROADBLOCK - HOLD THIS LANE" : "ROADBLOCK - GET TO THE GAP";
        _warningText.color = inGap ? new Color(0.4f, 1f, 0.5f) : RuntimeUI.Gold;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 12f);
        for (int i = 0; i < _laneMarks.Length; i++)
        {
            Color c = i == next.gap ? new Color(0.25f, 0.95f, 0.4f, 0.95f) : new Color(1f, 0.2f, 0.15f, 0.8f);
            if (i == _claire.CurrentLane && !inGap) c = Color.Lerp(c, Color.white, pulse * 0.6f);
            _laneMarks[i].color = c;
        }
    }
}

/// <summary>One lane of a roadblock. When it is removed (hit, or cleared for a rescue) its car goes with it.</summary>
public class RoadblockLane : MonoBehaviour
{
    public GameObject car;

    private void OnDestroy()
    {
        if (car != null) Destroy(car);
    }
}
