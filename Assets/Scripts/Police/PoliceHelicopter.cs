using UnityEngine;

/// <summary>
/// Scenery with a purpose: a police helicopter that flies in over the road ahead when police pressure builds
/// and peels away when it drops, so the meter can be read from the world. It has no gameplay effect.
/// </summary>
public class PoliceHelicopter : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        GameStartFlow.RegisterBootstrap(flow =>
        {
            if (flow.GetComponent<PoliceHelicopter>() == null)
                flow.gameObject.AddComponent<PoliceHelicopter>();
        });
    }

    [Range(0f, 1f)] public float engageAtPressure = 0.45f;
    [Range(0f, 1f)] public float releaseAtPressure = 0.25f;
    public float hoverHeight = 8.5f;
    public float hoverAhead = 62f;
    public float swayWidth = 5f;
    public float smoothTime = 1.6f;

    [Range(0f, 1f)] public float maxVolume = 0.55f;

    private Transform _beam;
    private AudioSource _audio;
    private Transform _heli;
    private PlayerLaneMovement _claire;
    private bool _engaged;
    private Vector3 _velocity;

    private void Start()
    {
        _claire = FindFirstObjectByType<PlayerLaneMovement>();
        var set = Resources.Load<PoliceSet>("PoliceSet");
        if (set == null || set.helicopter == null || _claire == null) return;

        var go = Instantiate(set.helicopter);
        go.name = "Police Helicopter";
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);
        go.SetActive(false);
        _audio = ProceduralSfx.AttachHelicopterSound(go);
        _heli = go.transform;
        CreateSearchlight();
    }

    /// <summary>A visible beam plus a real spot light that tracks Claire.</summary>
    private void CreateSearchlight()
    {
        var fx = Resources.Load<Material>("FxParticle");
        if (fx == null) return;

        var mesh = new Mesh { name = "Searchlight Cone" };
        const int sides = 16;
        var vertices = new Vector3[sides * 2];
        var triangles = new int[sides * 6];
        for (int i = 0; i < sides; i++)
        {
            float a = i / (float)sides * Mathf.PI * 2f;
            var ring = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            vertices[i * 2] = ring * 0.03f;
            vertices[i * 2 + 1] = ring * 0.14f + Vector3.forward;
            int next = (i + 1) % sides;
            int t = i * 6;
            triangles[t] = i * 2; triangles[t + 1] = next * 2; triangles[t + 2] = i * 2 + 1;
            triangles[t + 3] = next * 2; triangles[t + 4] = next * 2 + 1; triangles[t + 5] = i * 2 + 1;
        }
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();

        var beam = new GameObject("Searchlight");
        beam.AddComponent<MeshFilter>().sharedMesh = mesh;
        var material = new Material(fx);
        material.SetColor("_BaseColor", new Color(1f, 0.96f, 0.8f, 0.16f));
        material.SetFloat("_Cull", 0f);
        beam.AddComponent<MeshRenderer>().sharedMaterial = material;
        beam.SetActive(false);
        _beam = beam.transform;

        var spot = new GameObject("Spot").AddComponent<Light>();
        spot.transform.SetParent(_beam, false);
        spot.type = LightType.Spot;
        spot.spotAngle = 16f;
        spot.range = 120f;
        spot.intensity = 60f;
        spot.color = new Color(1f, 0.96f, 0.8f);
    }

    private void LateUpdate()
    {
        if (_beam == null || _heli == null) return;
        bool on = _heli.gameObject.activeSelf && _engaged;
        if (_beam.gameObject.activeSelf != on) _beam.gameObject.SetActive(on);
        if (!on) return;

        Vector3 from = _heli.position + Vector3.down * 1.2f;
        Vector3 to = _claire.transform.position;
        _beam.position = from;
        _beam.rotation = Quaternion.LookRotation(to - from);
        _beam.localScale = new Vector3(Vector3.Distance(from, to), Vector3.Distance(from, to), Vector3.Distance(from, to));
    }

    private void Update()
    {
        if (_heli == null) return;

        if (!GameStartFlow.IsGameplayActive)
        {
            if (_audio.isPlaying) _audio.Pause();
            return;
        }
        if (_heli.gameObject.activeSelf)
        {
            if (!_audio.isPlaying) _audio.UnPause();
            float closeness = Mathf.Clamp01(1f - Vector3.Distance(_heli.position, _claire.transform.position) / 170f);
            _audio.volume = maxVolume * closeness * GameSettings.SfxVolume;
        }

        float pressure = PolicePressure.Instance != null ? PolicePressure.Instance.Normalized : 0f;
        Vector3 claire = _claire.transform.position;

        if (!_engaged && pressure >= engageAtPressure)
        {
            _engaged = true;
            if (!_heli.gameObject.activeSelf)
            {
                _heli.position = new Vector3(Random.value < 0.5f ? -45f : 45f, hoverHeight + 22f, claire.z + 110f);
                _velocity = Vector3.zero;
                _heli.gameObject.SetActive(true);
            }
        }
        else if (_engaged && pressure <= releaseAtPressure)
        {
            _engaged = false;
        }

        if (!_heli.gameObject.activeSelf) return;

        Vector3 target = _engaged
            ? new Vector3(Mathf.Sin(Time.time * 0.4f) * swayWidth, hoverHeight, claire.z + hoverAhead)
            : new Vector3(_heli.position.x, hoverHeight + 35f, claire.z + 170f);

        _heli.position = Vector3.SmoothDamp(_heli.position, target, ref _velocity, smoothTime);
        // Faces the runners, nose down a little, banking into sideways drift.
        _heli.rotation = Quaternion.Euler(10f, 180f, Mathf.Clamp(_velocity.x * 2.5f, -22f, 22f));

        if (!_engaged && Vector3.Distance(_heli.position, target) < 15f)
            _heli.gameObject.SetActive(false);
    }
}
