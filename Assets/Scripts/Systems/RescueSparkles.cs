using UnityEngine;

/// <summary>
/// The rescue moment: blue sparkles stream from Claire into AJ, giving him the energy to run again,
/// then trail off him for as long as his post-rescue protection lasts.
/// </summary>
public class RescueSparkles : MonoBehaviour
{
    private const float BodyHeight = 1.1f;
    private const float StreamSeconds = 1.2f;
    private const float StreamRate = 130f;
    private const float StreamSpeed = 26f;
    private const float AuraRate = 55f;
    private const float AuraRadius = 0.6f;
    private const int BurstCount = 40;
    private const float ParticleCleanupSeconds = 1f;

    private Transform _aj, _claire;
    private float _life, _age, _streamDebt, _auraDebt;
    private ParticleSystem _system;
    private Material _material;

    public static void Create(Transform aj, Transform claire, float seconds)
    {
        var fx = Resources.Load<Material>("FxParticle");
        if (fx == null || aj == null) return;

        var go = new GameObject("Rescue Sparkles");
        go.SetActive(false); // modules can only be configured before the system starts
        var root = go.AddComponent<RescueSparkles>();
        root._aj = aj;
        root._claire = claire;
        root._life = seconds;
        root.BuildSystem(fx);
        go.SetActive(true);
        root.Burst();
    }

    private void BuildSystem(Material fx)
    {
        _system = gameObject.AddComponent<ParticleSystem>();
        _material = new Material(fx);
        _material.SetTexture("_BaseMap", Star());
        _material.SetColor("_BaseColor", new Color(0.35f, 0.75f, 1f, 1f));
        GetComponent<ParticleSystemRenderer>().sharedMaterial = _material;

        var main = _system.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = false;
        main.maxParticles = 500;
        var emission = _system.emission;
        emission.enabled = false;
        var shape = _system.shape;
        shape.enabled = false;
        var fade = _system.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
        fade.color = gradient;
    }

    private void OnDestroy()
    {
        if (_material != null) Destroy(_material);
    }

    private Vector3 AJBody => _aj.position + Vector3.up * BodyHeight;

    private void Burst()
    {
        for (int i = 0; i < BurstCount; i++)
            Emit(AJBody, Random.onUnitSphere * Random.Range(1.5f, 5f), 0.5f, Random.Range(0.15f, 0.35f));
    }

    private void Emit(Vector3 position, Vector3 velocity, float lifetime, float size)
    {
        var p = new ParticleSystem.EmitParams
        {
            position = position,
            velocity = velocity,
            startLifetime = lifetime,
            startSize = size,
            startColor = Color.Lerp(Color.white, new Color(0.55f, 0.85f, 1f), Random.value)
        };
        _system.Emit(p, 1);
    }

    private void Update()
    {
        var flow = GameStartFlow.Instance;
        if (_aj == null || flow == null || flow.State == GameStartFlow.RunState.Results)
        {
            Destroy(gameObject);
            return;
        }

        _age += Time.deltaTime;
        if (_age >= _life + ParticleCleanupSeconds)
        {
            Destroy(gameObject);
            return;
        }

        if (_age < StreamSeconds && _claire != null) EmitStream();
        if (_age < _life) EmitAura();
    }

    private void EmitStream()
    {
        _streamDebt += StreamRate * Time.deltaTime;
        Vector3 from = _claire.position + Vector3.up * BodyHeight;
        for (; _streamDebt >= 1f; _streamDebt -= 1f)
        {
            Vector3 start = from + Random.insideUnitSphere * 0.3f;
            Vector3 toAJ = AJBody - start;
            float lifetime = Mathf.Clamp(toAJ.magnitude / StreamSpeed, 0.15f, 0.5f);
            Emit(start, toAJ / lifetime, lifetime, Random.Range(0.12f, 0.28f));
        }
    }

    private void EmitAura()
    {
        _auraDebt += AuraRate * Time.deltaTime;
        for (; _auraDebt >= 1f; _auraDebt -= 1f)
        {
            Vector2 ring = Random.insideUnitCircle.normalized * AuraRadius;
            Vector3 position = _aj.position + new Vector3(ring.x, Random.Range(0.1f, 1.9f), ring.y);
            Emit(position, Vector3.up * Random.Range(0.4f, 1.4f), Random.Range(0.4f, 0.8f), Random.Range(0.08f, 0.2f));
        }
    }

    private static Texture2D s_star;

    /// <summary>A bright centre with four thin points.</summary>
    public static Texture2D Star()
    {
        if (s_star != null) return s_star;
        const int size = 48;
        s_star = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs((x + 0.5f) / size * 2f - 1f);
                float dy = Mathf.Abs((y + 0.5f) / size * 2f - 1f);
                float core = Mathf.Exp(-(dx * dx + dy * dy) * 30f);
                float points = Mathf.Max(Mathf.Exp(-dy * 45f) * (1f - dx), Mathf.Exp(-dx * 45f) * (1f - dy));
                s_star.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(core + points)));
            }
        }
        s_star.Apply();
        return s_star;
    }
}
