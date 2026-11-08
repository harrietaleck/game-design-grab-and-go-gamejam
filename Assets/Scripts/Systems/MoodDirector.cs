using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// The look of a late-afternoon getaway: low warm sun, distance haze, a filmic grade with bloom on the police lights,
/// plus dust at Claire's feet and leaves blowing down the street.
/// </summary>
public class MoodDirector : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        GameStartFlow.RegisterBootstrap(flow =>
        {
            if (flow.GetComponent<MoodDirector>() == null)
                flow.gameObject.AddComponent<MoodDirector>();
        });
    }

    private PlayerLaneMovement _claire;
    private ParticleSystem _dust;
    private Transform _weather;
    private Texture2D _softDot;
    private VolumeProfile _profile;

    /// <summary>Applies the lighting and grade to the current scene. Also used by the front end.</summary>
    public static VolumeProfile ApplyLook()
    {
        foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (light.type != LightType.Directional) continue;
            light.transform.rotation = Quaternion.Euler(34f, 318f, 0f);
            light.color = new Color(1f, 0.87f, 0.68f);
            light.intensity = 1.55f;
            light.shadowStrength = 0.72f;
        }

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.68f, 0.78f, 0.95f);
        RenderSettings.fogDensity = 0.0055f;

        if (Camera.main != null)
            Camera.main.GetUniversalAdditionalCameraData().renderPostProcessing = true;

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        var bloom = profile.Add<Bloom>();
        bloom.intensity.Override(0.55f);
        bloom.threshold.Override(1.05f);
        bloom.scatter.Override(0.6f);
        var grade = profile.Add<ColorAdjustments>();
        grade.saturation.Override(14f);
        grade.contrast.Override(12f);
        grade.colorFilter.Override(new Color(1f, 0.97f, 0.93f));
        var vignette = profile.Add<Vignette>();
        vignette.intensity.Override(0.24f);
        vignette.smoothness.Override(0.45f);
        profile.Add<Tonemapping>().mode.Override(TonemappingMode.Neutral);

        var volume = new GameObject("Mood Volume").AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 10f;
        volume.sharedProfile = profile;
        return profile;
    }

    private void Start()
    {
        _profile = ApplyLook();
        _claire = FindFirstObjectByType<PlayerLaneMovement>();
        var fx = Resources.Load<Material>("FxParticle");
        if (_claire == null || fx == null) return;

        _softDot = CreateSoftDot();
        _dust = CreateDust(fx);
        _weather = CreateLeaves(fx).transform;
    }

    private void OnDestroy()
    {
        if (_softDot != null) Destroy(_softDot);
        if (_profile != null) Destroy(_profile);
    }

    private void Update()
    {
        if (_claire == null || _dust == null) return;
        Vector3 c = _claire.transform.position;
        _dust.transform.position = new Vector3(c.x, c.y - _claire.RideHeight + 0.05f, c.z - 0.3f);
        _weather.position = new Vector3(0f, c.y + 6f, c.z + 22f);
        var emission = _dust.emission;
        emission.enabled = GameStartFlow.IsGameplayActive && _claire.HeightAboveGround <= 0f;
    }

    private static Texture2D CreateSoftDot()
    {
        const int size = 32;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), Vector2.one * (size * 0.5f)) / (size * 0.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.SmoothStep(1f, 0f, d)));
            }
        }
        tex.Apply();
        return tex;
    }

    private ParticleSystem CreateSystem(string name, Material fx, Color color)
    {
        var go = new GameObject(name);
        go.SetActive(false); // modules can only be configured before the system starts
        var system = go.AddComponent<ParticleSystem>();
        var material = new Material(fx);
        material.SetTexture("_BaseMap", _softDot);
        material.SetColor("_BaseColor", color);
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
        return system;
    }

    private ParticleSystem CreateDust(Material fx)
    {
        var system = CreateSystem("Runner Dust", fx, new Color(0.85f, 0.78f, 0.66f, 0.3f));
        var main = system.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = 0.55f;
        main.startSpeed = 0.7f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.25f);
        main.maxParticles = 60;
        var emission = system.emission;
        emission.rateOverTime = 26f;
        var shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.15f;
        var fade = system.colorOverLifetime;
        fade.enabled = true;
        fade.color = Fade();
        var grow = system.sizeOverLifetime;
        grow.enabled = true;
        grow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 1.8f));
        system.gameObject.SetActive(true);
        return system;
    }

    private ParticleSystem CreateLeaves(Material fx)
    {
        var system = CreateSystem("Street Leaves", fx, new Color(1f, 0.82f, 0.45f, 0.85f));
        var main = system.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = 5f;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.15f);
        main.gravityModifier = 0.03f;
        main.maxParticles = 150;
        var emission = system.emission;
        emission.rateOverTime = 26f;
        var shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(30f, 10f, 45f);
        var drift = system.velocityOverLifetime;
        drift.enabled = true;
        drift.space = ParticleSystemSimulationSpace.World;
        drift.x = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
        drift.y = new ParticleSystem.MinMaxCurve(-0.6f, 0.2f);
        drift.z = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);
        var fade = system.colorOverLifetime;
        fade.enabled = true;
        fade.color = Fade();
        system.gameObject.SetActive(true);
        return system;
    }

    private static Gradient Fade()
    {
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
        return g;
    }
}
