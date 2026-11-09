using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Police roadblocks: patrol cars parked across three of the four lanes, leaving one open gap.
/// They replace a normal traffic row (so spacing and reachability rules still apply), use LaneObstacle triggers
/// (so AJ's dodging, the rescue and officer fairness all work unchanged), and are announced on the HUD with the
/// open lane well before Claire reaches them. Claire reaching a closed lane ends the run.
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
    public float firstRoadblockZ = 120f;
    [Tooltip("Distance between roadblocks: first gap, last gap.")]
    public Vector2 spacingRange = new Vector2(115f, 90f);
    [Tooltip("The final roadblock is doubled: a second line this far behind the first.")]
    public float finalLineGap = 24f;
    [Tooltip("Distance from the last roadblock to the getaway van.")]
    public float vanDistance = 100f;
    public Vector2 hazardSpacing = new Vector2(55f, 32f);
    [Tooltip("The HUD warning appears when Claire is this far away.")]
    public float warningDistance = 48f;
    public float carScale = 0.95f;
    public int guardsPerRoadblock = 2;
    [Tooltip("How far in front of the cars (towards the runners) the guards stand.")]
    public float guardsInFront = 4.2f;

    private class Entry
    {
        public Transform root;
        public float z;
        public int gap;
        public int stage;       // 1..5
        public bool[] blocked;
        public bool passed;
    }

    public const int TotalRoadblocks = 5;
    // Lanes closed and guards posted at each line. The sixth line is the second half of roadblock 5.
    private static readonly int[] LanesClosed = { 2, 3, 3, 3, 3, 3 };
    private static readonly int[] GuardsAt = { 1, 2, 2, 3, 3, 3 };
    private static readonly float[] WarningAt = { 1f, 1f, 0.92f, 0.85f, 0.75f, 0.75f };

    public static RoadblockDirector Instance { get; private set; }
    /// <summary>True when the run is won by reaching the van rather than by the clock.</summary>
    public bool MissionActive { get; private set; }
    public int RoadblocksPassed { get; private set; }
    public GetawayVan Van { get; private set; }

    private const float BreatherLength = 30f;      // open road after each roadblock
    private const float BreatherSeconds = 5f;
    private const float ThreatClearDistance = 50f; // no new police this close to a roadblock
    private const float SpeedPerRoadblock = 0.0625f; // 6 -> 7.5 after all five
    private float _breatherUntil;

    /// <summary>False close to a roadblock and just after one, so threats don't all land at once.</summary>
    public bool ThreatWindowOpen
    {
        get
        {
            if (Time.time < _breatherUntil || _claire == null) return false;
            float cz = _claire.transform.position.z;
            foreach (var e in _active)
                if (e.z > cz && e.z - cz < ThreatClearDistance) return false;
            return true;
        }
    }

    private void OnRoadblockCleared()
    {
        _breatherUntil = Time.time + BreatherSeconds;
        if (PolicePressure.Instance != null) PolicePressure.Instance.Relieve(25f, 2f);

        // Faster after each block. Police scale too, so chasers can still catch up.
        float factor = (1f + SpeedPerRoadblock * RoadblocksPassed) / (1f + SpeedPerRoadblock * (RoadblocksPassed - 1));
        _claire.forwardSpeed *= factor;
        var police = PoliceDirector.Instance;
        if (police != null)
        {
            if (police.AJ != null) police.AJ.forwardSpeed *= factor;
            police.chaserSpeed *= factor;
            police.pursuerSpeed *= factor;
            police.dogSpeed *= factor;
        }
    }

    private ThiefCollectibleDropper _dropper;

    private void PlaceCoin(float x, float y, float z, Transform parent)
    {
        if (_dropper == null) _dropper = FindFirstObjectByType<ThiefCollectibleDropper>();
        if (_dropper != null) _dropper.SpawnTrailCoin(new Vector3(x, y, z), parent);
    }

    /// <summary>Coins lead into the open lane; a reward line and a first-aid kit wait just past it.</summary>
    private void PlaceRoadblockRewards(int line, int gap, float z, Transform tile)
    {
        // The coins would mean nothing if a car was parked on them.
        float x = LaneMath.SharedLaneX(gap);
        foreach (var o in FindObjectsByType<LaneObstacle>(FindObjectsSortMode.None))
        {
            float behind = z - o.transform.position.z;
            if (behind > 0.5f && behind < 30f && !o.passableByRunners && LaneMath.SharedNearestLane(o.transform.position.x) == gap)
                Destroy(o.gameObject);
        }

        for (float d = 24f; d >= 8f; d -= 4f) PlaceCoin(x, 0.6f, z - d, tile);
        if (line == 4) return; // the second half of the final roadblock comes straight after

        for (float d = 6f; d <= 26f; d += 4f) if (Mathf.Abs(d - 14f) > 2.5f) PlaceCoin(x, 0.6f, z + d, tile);
        EnsureHazardMaterials();
        HealthPickup.Create(new Vector3(x, 1f, z + 14f), tile, _whiteMaterial, _redMaterial);
    }

    /// <summary>Goal text for the HUD.</summary>
    public string ProgressText
    {
        get
        {
            if (RoadblocksPassed < TotalRoadblocks || Van == null) return $"BLOCKS {RoadblocksPassed}/{TotalRoadblocks}";
            return $"VAN {Mathf.Max(0, Mathf.CeilToInt(Van.FinishZ - _claire.transform.position.z))}M";
        }
    }

    private int _lines;
    private float _lastLineZ;
    private float _nextHazardZ = 62f;
    private bool _nextHazardIsSlide;
    private Material _spikeMaterial, _steelMaterial, _redMaterial, _whiteMaterial;

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
        Instance = this;
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
        {
            _spawner.RowOverride = PlaceRow;
            MissionActive = true;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_spawner != null) _spawner.RowOverride = null;
        foreach (var m in new[] { _lightMaterial, _spikeMaterial, _steelMaterial, _redMaterial, _whiteMaterial })
            if (m != null) Destroy(m);
    }

    /// <summary>Decides what each new road row holds: a roadblock, a jump or slide hazard, the van, or normal traffic.</summary>
    private bool[] PlaceRow(float z, Transform tile, bool[] previousFree)
    {
        if (_lines >= LanesClosed.Length)
        {
            if (Van == null && z >= _lastLineZ + vanDistance)
                Van = GetawayVan.Create(z, _claire, _set);
            if (z > _lastLineZ + 24f) return AllFree(); // a clear road home
        }

        var block = TryPlaceRoadblock(z, tile, previousFree);
        if (block != null) return block;
        if (_lines > 0 && z > _lastLineZ && z <= _lastLineZ + BreatherLength) return AllFree();

        bool clearOfRoadblocks = z > _lastLineZ + 18f && (_lines >= LanesClosed.Length || z < _nextZ - 18f);
        if (z >= _nextHazardZ && clearOfRoadblocks && _lines < LanesClosed.Length)
        {
            PlaceHazard(z, tile);
            return AllFree();
        }
        return null;
    }

    private static bool[] AllFree()
    {
        var free = new bool[LaneMath.SharedLaneCount];
        for (int i = 0; i < free.Length; i++) free[i] = true;
        return free;
    }

    // ---------------- Jump and slide hazards ----------------

    private void PlaceHazard(float z, Transform tile)
    {
        float progress = Mathf.Clamp01(_lines / (float)LanesClosed.Length);
        _nextHazardZ = z + Mathf.Lerp(hazardSpacing.x, hazardSpacing.y, progress);
        bool slide = _nextHazardIsSlide;
        _nextHazardIsSlide = !slide;

        // Early hazards leave room to go round; late ones span the whole road, so you must use the move.
        int laneCount = LaneMath.SharedLaneCount;
        int span = Mathf.Min(laneCount, 2 + _lines / 2);
        int first = Random.Range(0, laneCount - span + 1);
        EnsureHazardMaterials();

        var root = new GameObject(slide ? "Barrier Arm" : "Spike Strip").transform;
        root.SetParent(tile, false);
        root.position = new Vector3(0f, _spawner.obstacleY, z);
        for (int lane = first; lane < first + span; lane++)
            AddHazardLane(root, lane, z, slide);

        float midX = (LaneMath.SharedLaneX(first) + LaneMath.SharedLaneX(first + span - 1)) * 0.5f;
        var label = new GameObject("Hazard Label").AddComponent<TextMeshPro>();
        label.text = slide ? "SLIDE!" : "JUMP!";
        label.fontSize = 5f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = slide ? new Color(1f, 0.45f, 0.3f) : new Color(1f, 0.85f, 0.2f);
        label.rectTransform.sizeDelta = new Vector2(10f, 3f);
        label.transform.SetParent(root, false);
        label.transform.position = new Vector3(midX, root.position.y + (slide ? 2.9f : 1.5f), z);
        label.gameObject.AddComponent<FaceCamera>();

        // Coins show the move: an arc over spikes, a low line under the barrier.
        float coinX = LaneMath.SharedLaneX(first + span / 2);
        for (int i = -2; i <= 2; i++)
        {
            float arc = slide ? 0f : 1f - i * i / 6.25f;
            PlaceCoin(coinX, 0.6f + arc, z + i * 1.5f, tile);
        }
    }

    private void EnsureHazardMaterials()
    {
        if (_spikeMaterial != null) return;
        var body = _set.policeCar.GetComponentInChildren<Renderer>(true);
        _spikeMaterial = Tinted(body, new Color(0.1f, 0.1f, 0.12f));
        _steelMaterial = Tinted(body, new Color(0.8f, 0.82f, 0.88f));
        _redMaterial = Tinted(body, new Color(0.9f, 0.12f, 0.1f));
        _whiteMaterial = Tinted(body, new Color(0.95f, 0.95f, 0.95f));
    }

    private static Material Tinted(Renderer source, Color color)
    {
        var m = new Material(source.sharedMaterial);
        m.SetTexture("_BaseMap", null);
        m.SetColor("_BaseColor", color);
        return m;
    }

    private static void AddBox(Transform parent, Material material, Vector3 localPosition, Vector3 size, PrimitiveType shape = PrimitiveType.Cube)
    {
        var go = GameObject.CreatePrimitive(shape);
        Destroy(go.GetComponent<Collider>());
        go.GetComponent<Renderer>().sharedMaterial = material;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = size;
    }

    private void AddHazardLane(Transform root, int lane, float z, bool slide)
    {
        float width = LaneMath.SharedLaneDistance;
        var go = new GameObject(slide ? "Barrier Lane" : "Spike Lane");
        go.transform.SetParent(root, false);
        go.transform.position = new Vector3(LaneMath.SharedLaneX(lane), root.position.y, z);

        var obstacle = go.AddComponent<LaneObstacle>();
        obstacle.passableByRunners = true;
        obstacle.clearedByDodge = slide;
        obstacle.clearHeight = slide ? 99f : 0.35f;
        LaneObstacleSpawner.EnsureObstacleSetup(go);
        var box = go.GetComponent<BoxCollider>();
        box.size = new Vector3(width * 0.95f, 3f, 0.8f);
        box.center = Vector3.up * 1.5f;
        go.AddComponent<RunnerHazard>().slide = slide;

        if (slide)
        {
            const float armHeight = 1.3f;
            AddBox(go.transform, _spikeMaterial, new Vector3(-width * 0.5f, armHeight * 0.5f, 0f), new Vector3(0.14f, armHeight * 0.5f, 0.14f), PrimitiveType.Cylinder);
            const int stripes = 4;
            for (int i = 0; i < stripes; i++)
            {
                float x = (i + 0.5f) / stripes * width - width * 0.5f;
                AddBox(go.transform, i % 2 == 0 ? _redMaterial : _whiteMaterial, new Vector3(x, armHeight, 0f), new Vector3(width / stripes, 0.2f, 0.14f));
            }
        }
        else
        {
            AddBox(go.transform, _spikeMaterial, new Vector3(0f, 0.03f, 0f), new Vector3(width * 0.92f, 0.06f, 0.55f));
            const int spikes = 7;
            for (int i = 0; i < spikes; i++)
            {
                float x = (i + 0.5f) / spikes * width * 0.88f - width * 0.44f;
                AddBox(go.transform, _steelMaterial, new Vector3(x, 0.17f, 0f), new Vector3(0.07f, 0.13f, 0.07f), PrimitiveType.Capsule);
            }
        }
    }

    // ---------------- Placement ----------------

    private bool[] TryPlaceRoadblock(float z, Transform tile, bool[] previousFree)
    {
        if (_lines >= LanesClosed.Length || z < _nextZ) return null;

        int laneCount = LaneMath.SharedLaneCount;
        int gap = PickGap(laneCount, previousFree);
        if (gap < 0) return null;

        int line = _lines++;
        _lastLineZ = z;
        int stage = Mathf.Min(line + 1, TotalRoadblocks);
        _nextZ = line == 4 ? z + finalLineGap : z + Mathf.Lerp(spacingRange.x, spacingRange.y, line / 3f);

        // Close lanes starting with the ones next to the gap, so an open lane is a clear choice.
        var blocked = new bool[laneCount];
        int toClose = Mathf.Min(LanesClosed[line], laneCount - 1);
        var candidates = new List<int>();
        for (int i = 0; i < laneCount; i++) if (i != gap) candidates.Add(i);
        while (toClose-- > 0)
        {
            int pick = Random.Range(0, candidates.Count);
            blocked[candidates[pick]] = true;
            candidates.RemoveAt(pick);
        }

        var root = new GameObject("Police Roadblock").transform;
        root.SetParent(tile, false);
        root.position = new Vector3(0f, _spawner.obstacleY, z);
        root.gameObject.AddComponent<RoadblockStrobe>();

        // Adjacent closed lanes get one car parked across both; a single closed lane gets one car.
        for (int lane = 0; lane < laneCount; lane++)
        {
            if (!blocked[lane]) continue;
            bool pair = lane + 1 < laneCount && blocked[lane + 1];
            AddCar(root, lane, pair ? lane + 1 : lane, z);
            if (pair) lane++;
        }

        PostGuards(gap, blocked, GuardsAt[line], z);
        _active.Add(new Entry { root = root, z = z, gap = gap, stage = stage, blocked = blocked });
        PlaceRoadblockRewards(line, gap, z, tile);

        var free = new bool[laneCount];
        for (int i = 0; i < laneCount; i++) free[i] = !blocked[i];
        return free;
    }

    /// <summary>Officers in front of the cars, on the closed lanes nearest the gap.</summary>
    private void PostGuards(int gap, bool[] blocked, int count, float z)
    {
        var police = PoliceDirector.Instance;
        if (police == null) return;

        int posted = 0;
        for (int offset = 1; offset < blocked.Length && posted < count; offset++)
        {
            foreach (int lane in new[] { gap - offset, gap + offset })
            {
                if (lane < 0 || lane >= blocked.Length || !blocked[lane] || posted >= count) continue;
                if (police.SpawnGuard(lane, z - guardsInFront)) posted++;
            }
        }
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
        obstacle.catchesClaire = true;
        obstacle.clearHeight = 99f; // roadblocks cannot be jumped
        LaneObstacleSpawner.EnsureObstacleSetup(go);
        go.GetComponent<BoxCollider>().size = new Vector3(LaneMath.SharedLaneDistance * 0.75f, 8f, depth);
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
        var panel = RuntimeUI.CreateHudPanel(root, "RoadblockWarning (runtime)");
        panel.raycastTarget = false;
        RuntimeUI.Place(panel.rectTransform, new Vector2(1f, 0.5f), new Vector2(720f, 130f), new Vector2(-16f, 20f));
        SidePopup.Attach(panel.rectTransform, fromRight: true);
        _warningText = RuntimeUI.CreateText(panel.transform, "Text", "ROADBLOCK AHEAD", 26f, TextAlignmentOptions.Center, RuntimeUI.Gold, FontStyles.Bold);
        RuntimeUI.Place(_warningText.rectTransform, new Vector2(0.5f, 1f), new Vector2(660f, 56f), new Vector2(0f, -14f));
        RuntimeUI.FitOnOneLine(_warningText);
        _laneMarks = new Image[LaneMath.SharedLaneCount];
        for (int i = 0; i < _laneMarks.Length; i++)
        {
            var mark = RuntimeUI.CreatePanel(panel.transform, $"Lane{i}", Color.white);
            mark.raycastTarget = false;
            float x = (i - (_laneMarks.Length - 1) * 0.5f) * 130f;
            RuntimeUI.Place(mark.rectTransform, new Vector2(0.5f, 0f), new Vector2(110f, 30f), new Vector2(x, 22f));
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
            foreach (var e in _active)
            {
                if (!e.passed && e.z < cz - 1f)
                {
                    e.passed = true;
                    bool stageDone = !_active.Exists(o => o.stage == e.stage && !o.passed);
                    if (stageDone && _lines > e.stage - 1 && (e.stage < TotalRoadblocks || _lines >= LanesClosed.Length))
                    {
                        if (e.stage > RoadblocksPassed)
                        {
                            RoadblocksPassed = e.stage;
                            OnRoadblockCleared();
                        }
                        if (_flow.hudController != null)
                            _flow.hudController.ShowAlert(RoadblocksPassed >= TotalRoadblocks ? "ALL CLEAR! TO THE VAN!" : $"ROADBLOCK {RoadblocksPassed}/{TotalRoadblocks} CLEARED");
                    }
                }
            }
            _active.RemoveAll(e => e.root == null || e.passed);
            foreach (var e in _active)
            {
                float dz = e.z - cz;
                float range = warningDistance * WarningAt[Mathf.Clamp(e.stage - 1, 0, WarningAt.Length - 1)];
                if (dz > 0f && dz <= range && (next == null || e.z < next.z)) next = e;
            }
        }

        _warning.SetActive(next != null);
        if (next == null) return;

        if (next != _warned)
        {
            _warned = next;
            ProceduralSfx.Play(ProceduralSfx.Alarm, 0.45f);
        }

        bool inGap = !next.blocked[_claire.CurrentLane];
        _warningText.text = $"ROADBLOCK {next.stage}/{TotalRoadblocks} - {(inGap ? "HOLD THIS LANE" : "GET TO THE GAP")}";
        _warningText.color = inGap ? new Color(0.4f, 1f, 0.5f) : RuntimeUI.Gold;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 12f);
        for (int i = 0; i < _laneMarks.Length; i++)
        {
            Color c = !next.blocked[i] ? new Color(0.25f, 0.95f, 0.4f, 0.95f) : new Color(1f, 0.2f, 0.15f, 0.8f);
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
