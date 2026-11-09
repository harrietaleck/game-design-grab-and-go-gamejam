using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// The look of the getaway, set by difficulty: clear morning (Easy), late afternoon (Normal) or night (Hard).
/// Also adds dust at Claire's feet and leaves blowing down the street.
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
    private Transform _drift;
    private Texture2D _softDot;
    private VolumeProfile _profile;

    private class Look
    {
        public Vector3 sunAngles;
        public Color sunColor, fogColor, filter;
        public float sunIntensity, shadowStrength, fogDensity, bloom, bloomThreshold, saturation, contrast, vignette;
    }

    // Each difficulty is a time of day: the harder the run, the later it gets.
    private static readonly Look Morning = new Look
    {
        sunAngles = new Vector3(58f, 318f, 0f), sunColor = new Color(1f, 0.97f, 0.9f), sunIntensity = 1.35f, shadowStrength = 0.6f,
        fogColor = new Color(0.75f, 0.87f, 1f), fogDensity = 0.0035f,
        bloom = 0.3f, bloomThreshold = 1.1f, saturation = 8f, contrast = 5f, vignette = 0.12f, filter = Color.white
    };
    private static readonly Look Afternoon = new Look
    {
        sunAngles = new Vector3(34f, 318f, 0f), sunColor = new Color(1f, 0.87f, 0.68f), sunIntensity = 1.55f, shadowStrength = 0.72f,
        fogColor = new Color(0.68f, 0.78f, 0.95f), fogDensity = 0.0055f,
        bloom = 0.55f, bloomThreshold = 1.05f, saturation = 14f, contrast = 12f, vignette = 0.24f, filter = new Color(1f, 0.97f, 0.93f)
    };
    private static readonly Look Night = new Look
    {
        sunAngles = new Vector3(42f, 210f, 0f), sunColor = new Color(0.5f, 0.62f, 1f), sunIntensity = 0.4f, shadowStrength = 0.5f,
        fogColor = new Color(0.17f, 0.21f, 0.32f), fogDensity = 0.014f, // a cold, snowy haze
        bloom = 0.75f, bloomThreshold = 1f, saturation = 6f, contrast = 16f, vignette = 0.34f, filter = new Color(0.88f, 0.92f, 1f)
    };

    private static Material s_nightSky;

    /// <summary>The front end keeps the afternoon look; only a run follows the difficulty.</summary>
    private static GameSettings.DifficultyLevel CurrentLevel =>
        GameStartFlow.Instance != null ? GameSettings.Difficulty : GameSettings.DifficultyLevel.Normal;

    public static bool IsNight => CurrentLevel == GameSettings.DifficultyLevel.Hard;

    /// <summary>The sky for this run: the day sky, darkened at night.</summary>
    public static Material SkyFor(Material daySky)
    {
        if (!IsNight || daySky == null) return daySky;
        if (s_nightSky == null)
        {
            s_nightSky = new Material(daySky) { name = "Night Sky" };
            s_nightSky.SetColor("_Tint", new Color(0.14f, 0.18f, 0.36f));
            s_nightSky.SetFloat("_Exposure", 0.45f);
        }
        return s_nightSky;
    }

    /// <summary>Applies the lighting and grade to the current scene. Also used by the front end.</summary>
    public static VolumeProfile ApplyLook()
    {
        var level = CurrentLevel;
        var look = level == GameSettings.DifficultyLevel.Easy ? Morning : level == GameSettings.DifficultyLevel.Hard ? Night : Afternoon;

        foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (light.type != LightType.Directional) continue;
            light.transform.rotation = Quaternion.Euler(look.sunAngles);
            light.color = look.sunColor;
            light.intensity = look.sunIntensity;
            light.shadowStrength = look.shadowStrength;
        }

        if (look == Night)
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.24f, 0.27f, 0.42f);
            RenderSettings.reflectionIntensity = 0.3f;
        }

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = look.fogColor;
        RenderSettings.fogDensity = look.fogDensity;

        if (Camera.main != null)
            Camera.main.GetUniversalAdditionalCameraData().renderPostProcessing = true;

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        var bloom = profile.Add<Bloom>();
        bloom.intensity.Override(look.bloom);
        bloom.threshold.Override(look.bloomThreshold);
        bloom.scatter.Override(0.6f);
        var grade = profile.Add<ColorAdjustments>();
        grade.saturation.Override(look.saturation);
        grade.contrast.Override(look.contrast);
        grade.colorFilter.Override(look.filter);
        var vignette = profile.Add<Vignette>();
        vignette.intensity.Override(look.vignette);
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

        if (IsNight) BuildNightLights();
        _softDot = CreateSoftDot();
        _dust = CreateDust(fx);
        _weather = (IsNight ? CreateSnow(fx) : CreateLeaves(fx)).transform;
        if (IsNight) _drift = CreateGroundDrift(fx).transform;
    }

    private const float LampSpacing = 18f;
    private const float LampSideX = 8.5f;
    private Light[] _lamps;

    /// <summary>A beam from the camera keeps obstacles readable, and warm pools of light roll along the street.</summary>
    private void BuildNightLights()
    {
        if (Camera.main != null)
        {
            var beam = new GameObject("Night Beam").AddComponent<Light>();
            beam.transform.SetParent(Camera.main.transform, false);
            beam.type = LightType.Spot;
            beam.spotAngle = 70f;
            beam.range = 90f;
            beam.intensity = 90f;
            beam.color = new Color(0.95f, 0.95f, 1f);
            beam.shadows = LightShadows.None;
        }

        _lamps = new Light[6];
        for (int i = 0; i < _lamps.Length; i++)
        {
            var lamp = new GameObject("Street Pool Light").AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.range = 15f;
            lamp.intensity = 8f;
            lamp.color = new Color(1f, 0.78f, 0.48f);
            lamp.shadows = LightShadows.None;
            _lamps[i] = lamp;
        }
    }

    private void PlaceLamps(float claireZ)
    {
        float start = Mathf.Floor(claireZ / LampSpacing) * LampSpacing;
        for (int i = 0; i < _lamps.Length; i++)
        {
            float side = i % 2 == 0 ? -1f : 1f;
            _lamps[i].transform.position = new Vector3(side * LampSideX, 6f, start + (i / 2) * LampSpacing);
        }
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
        if (_lamps != null) PlaceLamps(c.z);
        _dust.transform.position = new Vector3(c.x, c.y - _claire.RideHeight + 0.05f, c.z - 0.3f);
        _weather.position = new Vector3(0f, c.y + (IsNight ? 9f : 6f), c.z + 22f);
        if (_drift != null) _drift.position = new Vector3(0f, c.y - _claire.RideHeight + 0.2f, c.z + 20f);
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
        var system = CreateSystem("Runner Dust", fx, IsNight ? new Color(0.85f, 0.9f, 1f, 0.4f) : new Color(0.85f, 0.78f, 0.66f, 0.3f));
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
        var system = CreateSystem("Street Leaves", fx, IsNight ? new Color(0.45f, 0.5f, 0.75f, 0.5f) : new Color(1f, 0.82f, 0.45f, 0.85f));
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

    /// <summary>Flakes falling through the street ahead of Claire.</summary>
    private ParticleSystem CreateSnow(Material fx)
    {
        var system = CreateSystem("Snowfall", fx, new Color(0.92f, 0.96f, 1f, 0.95f));
        var main = system.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = 5f;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.16f);
        main.maxParticles = 1000;
        var emission = system.emission;
        emission.rateOverTime = 170f;
        var shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(34f, 8f, 55f);
        var drift = system.velocityOverLifetime;
        drift.enabled = true;
        drift.space = ParticleSystemSimulationSpace.World;
        drift.x = new ParticleSystem.MinMaxCurve(0.2f, 1.6f);
        drift.y = new ParticleSystem.MinMaxCurve(-3.4f, -2f);
        drift.z = new ParticleSystem.MinMaxCurve(-1f, 1f);
        var fade = system.colorOverLifetime;
        fade.enabled = true;
        fade.color = Fade();
        system.gameObject.SetActive(true);
        return system;
    }

    /// <summary>Loose snow blowing low across the road.</summary>
    private ParticleSystem CreateGroundDrift(Material fx)
    {
        var system = CreateSystem("Ground Snow Drift", fx, new Color(0.85f, 0.9f, 1f, 0.22f));
        var main = system.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = 2.5f;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.3f);
        main.maxParticles = 150;
        var emission = system.emission;
        emission.rateOverTime = 45f;
        var shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(30f, 0.3f, 50f);
        var drift = system.velocityOverLifetime;
        drift.enabled = true;
        drift.space = ParticleSystemSimulationSpace.World;
        drift.x = new ParticleSystem.MinMaxCurve(3f, 6f);
        drift.y = new ParticleSystem.MinMaxCurve(0f, 0.2f);
        drift.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
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
