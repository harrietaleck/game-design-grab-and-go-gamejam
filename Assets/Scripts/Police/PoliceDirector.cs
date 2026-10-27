using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Spawns and manages police officers (chasers from behind, interceptors ahead, sidewalk officers stepping in).
/// Spawn rate and mix follow the police-pressure alert level. Officers are separate from LaneObstacle:
/// they move, react to the Distraction Pulse, grab Claire (damage) or AJ (caught -> rescue window).
/// Fairness: no officers in the first seconds, a capped number at once, and an officer never makes a row
/// with fewer than two free lanes (obstacles + officers).
/// </summary>
public class PoliceDirector : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        GameStartFlow.RegisterBootstrap(flow =>
        {
            if (flow.GetComponent<PoliceDirector>() == null)
                flow.gameObject.AddComponent<PoliceDirector>();
        });
    }

    public static PoliceDirector Instance { get; private set; }

    [Header("Spawning (scaled by police pressure 0..1)")]
    public bool policeEnabled = true;
    [Tooltip("No officers during the first seconds of a run.")]
    public float quietStartSeconds = 10f;
    public Vector2 spawnIntervalRange = new Vector2(9f, 3.5f);   // at pressure 0 -> 1
    public Vector2Int maxActiveRange = new Vector2Int(1, 4);     // at pressure 0 -> 1

    [Header("Movement")]
    public float chaserSpeed = 7.6f;        // Claire runs at 6
    [Tooltip("From this many seconds into a run, chasers can appear even at low pressure.")]
    public float chaserFromSeconds = 30f;
    public float chaserSpawnBehind = 28f;
    public float chaserRetargetDistance = 18f;
    public float interceptorSpeed = 1.8f;   // jogs towards the runners
    public float interceptorSpawnAhead = 60f;
    public float sidewalkSpawnAhead = 50f;
    public float sidewalkStepDistance = 16f;
    public float sidewalkStepSpeed = 4f;
    public float leaveSpeed = 2f;
    public float despawnBehind = 30f;
    public float despawnAhead = 160f;

    [Header("Grabs")]
    public float grabRadiusX = 1.1f;
    public float grabRadiusZ = 0.7f;
    public float claireGrabDamage = 15f;
    public float afterGrabStunSeconds = 1.5f;
    public float distractionStunSeconds = 3f;

    [Header("Look (stand-in officers)")]
    public Color uniformTint = new Color(0.55f, 0.68f, 1f);
    public Color capColor = new Color(0.08f, 0.1f, 0.22f);

    public PlayerLaneMovement Claire { get; private set; }
    public AIThiefLaneDodge AJ { get; private set; }
    public float SidewalkX { get; private set; } = 9f;
    public IReadOnlyList<PoliceOfficer> Officers => _officers;

    private readonly List<PoliceOfficer> _officers = new();
    private readonly List<GameObject> _templates = new();
    private GameStartFlow _flow;
    private Transform _root;
    private Transform _templateHolder;
    private float _nextSpawnAt;
    private PoliceOfficer _warnedChaser;
    private bool _spottedRaised;

    // HUD
    private GameObject _warning;
    private TextMeshProUGUI _warningText;
    private Image[] _laneMarks;

    private void Awake()
    {
        Instance = this;
        _flow = GetComponent<GameStartFlow>();
        BuildWarningUI();
    }

    private void Start()
    {
        Claire = FindFirstObjectByType<PlayerLaneMovement>();
        AJ = FindFirstObjectByType<AIThiefLaneDodge>();
        SidewalkX = LaneMath.SharedLaneCount * LaneMath.SharedLaneDistance * 0.5f + 3.5f;
        _root = new GameObject("Police (runtime)").transform;
        BuildTemplates();
    }

    private readonly List<Material> _createdMaterials = new();

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        // Runtime-created officer materials would otherwise leak on every restart (scene reload).
        foreach (var m in _createdMaterials) if (m != null) Destroy(m);
        _createdMaterials.Clear();
        _uniformCache.Clear();
    }
    private void OnEnable() => GameEvents.OnEvent += OnGameEvent;
    private void OnDisable() => GameEvents.OnEvent -= OnGameEvent;

    private void OnGameEvent(GameEventData e)
    {
        switch (e.type)
        {
            case GameEventType.RunStarted:
                _nextSpawnAt = Time.time + quietStartSeconds;
                _spottedRaised = false;
                break;
            case GameEventType.DistractionUsed:
                foreach (var o in _officers) if (o != null) o.Stun(distractionStunSeconds);
                break;
            case GameEventType.AJCaught:
                // Resolve every active encounter: officers stop being hazards and walk off, so nothing conflicts
                // with the rescue (RescueController stays the only authority). New spawns are already paused
                // because Update only spawns in RunState.Playing.
                foreach (var o in _officers) if (o != null && o.state != PoliceOfficer.State.Leaving) o.BeginLeaving();
                break;
            case GameEventType.AJRescued:
                // Officers around AJ are shaken off by the rescue.
                if (AJ != null)
                    foreach (var o in _officers)
                        if (o != null && Mathf.Abs(o.transform.position.z - AJ.transform.position.z) < 15f) o.Stun(1f);
                break;
        }
    }

    private void Update()
    {
        UpdateWarning();
        if (!policeEnabled || _flow == null || _flow.State != GameStartFlow.RunState.Playing || Claire == null || _templates.Count == 0)
            return;

        _officers.RemoveAll(o => o == null);
        float p = PolicePressure.Instance != null ? PolicePressure.Instance.Normalized : 0f;
        int maxActive = Mathf.RoundToInt(Mathf.Lerp(maxActiveRange.x, maxActiveRange.y, p));
        if (Time.time < _nextSpawnAt || CountDangerous() >= maxActive) return;

        _nextSpawnAt = Time.time + Mathf.Lerp(spawnIntervalRange.x, spawnIntervalRange.y, p) * Random.Range(0.8f, 1.2f);
        SpawnFor(p);
    }

    private int CountDangerous()
    {
        int n = 0;
        foreach (var o in _officers) if (o != null && o.IsDangerous) n++;
        return n;
    }

    // ---------------- Spawning ----------------

    private void SpawnFor(float pressure)
    {
        // Mix by alert level: calm -> sidewalk officers, rising -> interceptors, high -> chasers too.
        float sidewalkW = Mathf.Lerp(0.7f, 0.25f, pressure);
        float interceptW = Mathf.Lerp(0.3f, 0.35f, pressure);
        // Chasers appear from mid-run at any alert level (rarely when calm), more as pressure rises.
        bool midRun = _flow != null && _flow.TimeSurvived >= chaserFromSeconds;
        float chaserW = !midRun && pressure < 0.3f ? 0f : Mathf.Lerp(0.15f, 0.45f, pressure);
        float r = Random.value * (sidewalkW + interceptW + chaserW);
        var kind = r < sidewalkW ? PoliceOfficer.Kind.Sidewalk : r < sidewalkW + interceptW ? PoliceOfficer.Kind.Interceptor : PoliceOfficer.Kind.Chaser;

        float cz = Claire.transform.position.z;
        switch (kind)
        {
            case PoliceOfficer.Kind.Chaser:
                Spawn(kind, Claire.CurrentLane, new Vector3(LaneMath.SharedLaneX(Claire.CurrentLane), Claire.transform.position.y, cz - chaserSpawnBehind), 0f);
                break;
            case PoliceOfficer.Kind.Interceptor:
            {
                float z = cz + interceptorSpawnAhead;
                int lane = PickFairLane(z);
                if (lane < 0) return;
                Spawn(kind, lane, new Vector3(LaneMath.SharedLaneX(lane), Claire.transform.position.y, z), 0f);
                break;
            }
            case PoliceOfficer.Kind.Sidewalk:
            {
                float side = Random.value < 0.5f ? -1f : 1f;
                Spawn(kind, -1, new Vector3(side * SidewalkX, Claire.transform.position.y, cz + sidewalkSpawnAhead), side * SidewalkX);
                break;
            }
        }
    }

    private void Spawn(PoliceOfficer.Kind kind, int lane, Vector3 pos, float sidewalkX)
    {
        var template = _templates[Random.Range(0, _templates.Count)];
        var go = Instantiate(template, pos, Quaternion.identity, _root);
        go.name = $"Police Officer ({kind})";
        go.SetActive(true);
        var officer = go.AddComponent<PoliceOfficer>();
        officer.Init(this, kind, lane, sidewalkX);
        _officers.Add(officer);
    }

    public void Despawn(PoliceOfficer o)
    {
        _officers.Remove(o);
        if (o != null) Destroy(o.gameObject);
    }

    private int PickFairLane(float z)
    {
        var lanes = new List<int>();
        for (int i = 0; i < LaneMath.SharedLaneCount; i++) lanes.Add(i);
        for (int i = lanes.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (lanes[i], lanes[j]) = (lanes[j], lanes[i]); }
        foreach (int lane in lanes)
            if (LaneStaysFair(null, lane, z)) return lane;
        return -1;
    }

    /// <summary>
    /// True if putting an officer in 'lane' near 'z' leaves at least two lanes free of obstacles and other officers,
    /// and the lane itself has no obstacle right there.
    /// </summary>
    public bool LaneStaysFair(PoliceOfficer self, int lane, float z)
    {
        var blocked = new bool[LaneMath.SharedLaneCount];
        foreach (var obstacle in FindObjectsByType<LaneObstacle>(FindObjectsSortMode.None))
        {
            var op = obstacle.transform.position;
            if (Mathf.Abs(op.z - z) > 4f) continue;
            blocked[LaneMath.SharedNearestLane(op.x)] = true;
        }
        if (blocked[lane]) return false;
        foreach (var o in _officers)
        {
            if (o == null || o == self || !o.BlocksLaneForAI) continue;
            if (Mathf.Abs(o.transform.position.z - z) > 4f) continue;
            blocked[o.lane] = true;
        }
        blocked[lane] = true;
        int free = 0;
        foreach (bool b in blocked) if (!b) free++;
        return free >= 2;
    }

    /// <summary>Used by AJ's AI: is there a lane-blocking officer in 'lane' between z and z + range?</summary>
    public bool IsLaneBlockedByPolice(int lane, float fromZ, float range)
    {
        foreach (var o in _officers)
        {
            if (o == null || !o.BlocksLaneForAI || o.lane != lane) continue;
            float dz = o.transform.position.z - fromZ;
            if (dz >= 0f && dz <= range) return true;
        }
        return false;
    }

    /// <summary>Used by the rescue: remove officers from AJ's lane between Claire and AJ so the rescue is possible.</summary>
    public void ClearLane(int lane, float zMin, float zMax)
    {
        foreach (var o in _officers)
        {
            if (o == null) continue;
            float z = o.transform.position.z;
            if (z < zMin || z > zMax) continue;
            if (o.lane == lane || Mathf.Abs(o.transform.position.x - LaneMath.SharedLaneX(lane)) < LaneMath.SharedLaneDistance * 0.5f)
                o.BeginLeaving();
        }
    }

    // ---------------- Grabs ----------------

    public void OnOfficerGrabbedClaire(PoliceOfficer o)
    {
        if (_flow == null || !GameStartFlow.IsGameplayActive) return;
        _flow.ApplyClaireDamage(claireGrabDamage); // raises ClaireCollision -> pressure, flash, shake, dialogue
        if (_flow.hudController != null) _flow.hudController.ShowAlert("OFFICER GRAB!");
    }

    public void OnOfficerGrabbedAJ(PoliceOfficer o)
    {
        if (AJ == null || AJ.IsCaught) return;
        AJ.SetCaught("police"); // -> AJCaught state and the rescue window
    }

    // ---------------- Warning HUD (camera cannot see behind Claire) ----------------

    private void BuildWarningUI()
    {
        Transform root = _flow != null && _flow.hudPanel != null ? _flow.hudPanel.transform : transform;
        var panel = RuntimeUI.CreatePanel(root, "PoliceWarning (runtime)", new Color(0.05f, 0.08f, 0.35f, 0.8f));
        panel.raycastTarget = false;
        RuntimeUI.Place(panel.rectTransform, new Vector2(0.5f, 0f), new Vector2(520f, 96f), new Vector2(0f, 250f));
        _warningText = RuntimeUI.CreateText(panel.transform, "Text", "POLICE BEHIND!", 30f, TextAlignmentOptions.Center, Color.white, FontStyles.Bold);
        RuntimeUI.Place(_warningText.rectTransform, new Vector2(0.5f, 1f), new Vector2(500f, 44f), new Vector2(0f, -4f));
        _laneMarks = new Image[LaneMath.SharedLaneCount];
        for (int i = 0; i < _laneMarks.Length; i++)
        {
            var mark = RuntimeUI.CreatePanel(panel.transform, $"Lane{i}", new Color(1f, 1f, 1f, 0.25f));
            mark.raycastTarget = false;
            float x = (i - (_laneMarks.Length - 1) * 0.5f) * 110f;
            RuntimeUI.Place(mark.rectTransform, new Vector2(0.5f, 0f), new Vector2(90f, 28f), new Vector2(x, 10f));
            _laneMarks[i] = mark;
        }
        _warning = panel.gameObject;
        _warning.SetActive(false);
    }

    private void UpdateWarning()
    {
        if (_warning == null) return;
        PoliceOfficer nearest = null;
        if (Claire != null && GameStartFlow.IsGameplayActive)
        {
            float cz = Claire.transform.position.z;
            float best = float.MaxValue;
            foreach (var o in _officers)
            {
                if (o == null || o.kind != PoliceOfficer.Kind.Chaser || !o.IsDangerous) continue;
                float behind = cz - o.transform.position.z;
                if (behind > 0f && behind < chaserSpawnBehind + 2f && behind < best) { best = behind; nearest = o; }
            }
        }

        _warning.SetActive(nearest != null);
        if (nearest == null) return;

        if (nearest != _warnedChaser)
        {
            _warnedChaser = nearest;
            ProceduralSfx.Play(ProceduralSfx.Alarm, 0.45f);
            if (!_spottedRaised) { _spottedRaised = true; GameEvents.Raise(GameEventType.PoliceSpotted); }
        }
        bool sameLane = nearest.lane == Claire.CurrentLane;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 14f);
        _warningText.text = sameLane ? "POLICE BEHIND - CHANGE LANE!" : "POLICE BEHIND";
        for (int i = 0; i < _laneMarks.Length; i++)
        {
            bool officerLane = i == nearest.lane;
            bool claireLane = i == Claire.CurrentLane;
            _laneMarks[i].color = officerLane
                ? Color.Lerp(new Color(1f, 0.2f, 0.2f, 0.9f), Color.white, sameLane ? pulse * 0.5f : 0f)
                : claireLane ? new Color(0.1f, 0.9f, 1f, 0.8f) : new Color(1f, 1f, 1f, 0.2f);
        }
    }

    // ---------------- Stand-in officer templates ----------------

    private void BuildTemplates()
    {
        var set = Resources.Load<PoliceSet>("PoliceSet");
        _templateHolder = new GameObject("Police Templates (inactive)").transform;
        _templateHolder.gameObject.SetActive(false);

        if (set != null && set.officerPrefabs != null && set.officerPrefabs.Length > 0)
        {
            foreach (var prefab in set.officerPrefabs)
            {
                if (prefab == null) continue;
                var t = Instantiate(prefab, _templateHolder);
                StripToVisual(t);
                if (set.tintPrefabs) TintUniform(t);
                _templates.Add(t);
            }
        }

        if (_templates.Count == 0)
        {
            // No police models in the project: build stand-ins from AJ's and Claire's rigs.
            foreach (var source in new[] { AJ != null ? AJ.gameObject : null, Claire != null ? Claire.gameObject : null })
            {
                if (source == null) continue;
                try { _templates.Add(MakeStandIn(source)); }
                catch (System.Exception e) { Debug.LogException(e); }
            }
            if (_templates.Count == 0) Debug.LogWarning("[PoliceDirector] No officer templates could be built; police disabled.");
        }
    }

    private GameObject MakeStandIn(GameObject source)
    {
        // Instantiating under an inactive parent means none of the source's scripts Awake on the copy.
        var copy = Instantiate(source, source.transform.position, source.transform.rotation, _templateHolder);
        copy.transform.localScale = source.transform.lossyScale;
        StripToVisual(copy);
        TintUniform(copy);
        AddCap(copy);
        AddTag(copy);
        return copy;
    }

    /// <summary>Removes gameplay scripts, physics and audio so the copy is just an animated model.</summary>
    private static void StripToVisual(GameObject go)
    {
        // Reverse order + two passes: components with [RequireComponent] dependants must go after them.
        for (int pass = 0; pass < 2; pass++)
        {
            var behaviours = go.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = behaviours.Length - 1; i >= 0; i--)
            {
                var mb = behaviours[i];
                if (mb == null || mb is TMP_Text) continue;
                try { DestroyImmediate(mb); } catch { /* retried on the next pass */ }
            }
        }
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
        foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true)) DestroyImmediate(rb);
        foreach (var a in go.GetComponentsInChildren<AudioSource>(true)) DestroyImmediate(a);
    }

    private readonly Dictionary<Material, Material> _uniformCache = new();

    private void TintUniform(GameObject go)
    {
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;
                if (!_uniformCache.TryGetValue(mats[i], out var tinted) || tinted == null)
                {
                    tinted = new Material(mats[i]) { name = mats[i].name + " (police)" };
                    _createdMaterials.Add(tinted);
                    if (tinted.HasProperty("_BaseColor")) tinted.SetColor("_BaseColor", tinted.GetColor("_BaseColor") * uniformTint);
                    if (tinted.HasProperty("_Color")) tinted.SetColor("_Color", tinted.GetColor("_Color") * uniformTint);
                    _uniformCache[mats[i]] = tinted;
                }
                mats[i] = tinted;
            }
            r.sharedMaterials = mats;
        }
    }

    private void AddCap(GameObject officer)
    {
        Transform head = FindByNameContains(officer.transform, "Head");
        var skinned = officer.GetComponentInChildren<Renderer>(true);
        if (head == null || skinned == null) return;

        var capMat = new Material(skinned.sharedMaterial) { name = "Police Cap" };
        _createdMaterials.Add(capMat);
        if (capMat.HasProperty("_BaseMap")) capMat.SetTexture("_BaseMap", null);
        if (capMat.HasProperty("_BaseColor")) capMat.SetColor("_BaseColor", capColor);
        if (capMat.HasProperty("_Color")) capMat.SetColor("_Color", capColor);

        var cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cap.name = "Police Cap";
        DestroyImmediate(cap.GetComponent<Collider>());
        cap.GetComponent<Renderer>().sharedMaterial = capMat;
        cap.transform.SetParent(head, false);
        SetWorldTransform(cap.transform, head.position + Vector3.up * 0.2f, new Vector3(0.27f, 0.06f, 0.27f));

        var visor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visor.name = "Visor";
        DestroyImmediate(visor.GetComponent<Collider>());
        visor.GetComponent<Renderer>().sharedMaterial = capMat;
        visor.transform.SetParent(head, false);
        SetWorldTransform(visor.transform, head.position + Vector3.up * 0.16f + officer.transform.forward * 0.15f, new Vector3(0.22f, 0.02f, 0.12f));

        // Small red/blue flashing light on top of the cap so officers read instantly.
        var light = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        light.name = "Police Light";
        DestroyImmediate(light.GetComponent<Collider>());
        var lightMat = new Material(capMat) { name = "Police Light" };
        _createdMaterials.Add(lightMat);
        light.GetComponent<Renderer>().sharedMaterial = lightMat;
        light.transform.SetParent(head, false);
        SetWorldTransform(light.transform, head.position + Vector3.up * 0.27f, new Vector3(0.1f, 0.1f, 0.1f));
        light.AddComponent<PoliceLightBlink>();
    }

    private static void SetWorldTransform(Transform t, Vector3 worldPos, Vector3 worldSize)
    {
        t.position = worldPos;
        t.rotation = Quaternion.identity;
        var parentScale = t.parent != null ? t.parent.lossyScale : Vector3.one;
        t.localScale = new Vector3(
            worldSize.x / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)),
            worldSize.y / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)),
            worldSize.z / Mathf.Max(0.0001f, Mathf.Abs(parentScale.z)));
    }

    private static void AddTag(GameObject officer)
    {
        var tagGo = new GameObject("Police Tag");
        tagGo.transform.SetParent(officer.transform, false);
        var text = tagGo.AddComponent<TextMeshPro>();
        text.text = "POLICE";
        text.fontSize = 4.5f;
        text.alignment = TextAlignmentOptions.Center;
        text.fontStyle = FontStyles.Bold;
        text.color = new Color(0.35f, 0.65f, 1f);
        // (No outline: TMP outline settings throw on objects that are still inactive, like these templates.)
        tagGo.AddComponent<FaceCamera>();
        SetWorldTransform(tagGo.transform, officer.transform.position + Vector3.up * 2.3f, Vector3.one);
    }

    private static Transform FindByNameContains(Transform root, string part)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name.EndsWith(":" + part) || t.name == part) return t;
        return null;
    }
}

/// <summary>Keeps world-space labels readable.</summary>
public class FaceCamera : MonoBehaviour
{
    private void LateUpdate()
    {
        var cam = Camera.main;
        if (cam != null) transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
    }
}

/// <summary>Alternates a small cap light between red and blue.</summary>
public class PoliceLightBlink : MonoBehaviour
{
    private Renderer _renderer;
    private MaterialPropertyBlock _block;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        _block = new MaterialPropertyBlock();
        if (_renderer != null) _renderer.sharedMaterial.EnableKeyword("_EMISSION");
    }

    private void Update()
    {
        if (_renderer == null) return;
        bool red = Mathf.Repeat(Time.time * 3f + transform.position.z * 0.1f, 1f) < 0.5f;
        var c = red ? new Color(1f, 0.1f, 0.1f) : new Color(0.1f, 0.35f, 1f);
        _block.SetColor(BaseColorId, c);
        _block.SetColor(EmissionId, c * 2f);
        _renderer.SetPropertyBlock(_block);
    }
}
