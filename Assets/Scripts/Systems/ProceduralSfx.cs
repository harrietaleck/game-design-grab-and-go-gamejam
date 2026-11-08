using UnityEngine;

/// <summary>
/// Tiny generated sound effects (no new audio assets): radio blip, distraction whoosh, alarm beep.
/// Clips are created once and cached.
/// </summary>
public static class ProceduralSfx
{
    private static AudioClip s_blip, s_pulse, s_alarm, s_rescue, s_gunshot, s_hit, s_helicopter, s_jump, s_dodge;
    private static AudioSource s_source;

    public static AudioClip RadioBlip => s_blip ??= Sweep("RadioBlip", 1400f, 1900f, 0.07f, 0.35f, square: true);
    public static AudioClip DistractionPulse => s_pulse ??= Sweep("DistractionPulse", 900f, 180f, 0.45f, 0.5f, square: false);
    public static AudioClip Alarm => s_alarm ??= TwoTone("Alarm", 880f, 660f, 0.6f, 0.35f);
    public static AudioClip Rescue => s_rescue ??= Sweep("Rescue", 400f, 1200f, 0.3f, 0.45f, square: false);

    public static AudioClip Gunshot => s_gunshot ??= Noise("Gunshot", 0.22f, 0.9f, 28f, 0.35f);
    public static AudioClip BulletHit => s_hit ??= Noise("BulletHit", 0.12f, 0.8f, 40f, 0.12f);

    public static AudioClip Jump => s_jump ??= Sweep("Jump", 280f, 760f, 0.16f, 0.4f, square: false);
    public static AudioClip Dodge => s_dodge ??= Noise("Dodge", 0.25f, 0.45f, 11f, 0.7f);

    /// <summary>A seamless looping helicopter chop.</summary>
    public static AudioClip HelicopterLoop => s_helicopter ??= Chop("HelicopterLoop", 1f, 11);

    /// <summary>Starts a looping helicopter sound on an object. It begins when the object is active.</summary>
    public static AudioSource AttachHelicopterSound(GameObject target)
    {
        var source = target.AddComponent<AudioSource>();
        source.clip = HelicopterLoop;
        source.loop = true;
        source.spatialBlend = 0f;
        source.volume = 0f;
        source.playOnAwake = true;
        if (target.activeInHierarchy) source.Play();
        return source;
    }

    /// <summary>Decaying filtered noise: 'smooth' 0..1 darkens it by low-pass filtering.</summary>
    private static AudioClip Noise(string name, float seconds, float volume, float decay, float smooth)
    {
        const int rate = 44100;
        int n = Mathf.CeilToInt(rate * seconds);
        var data = new float[n];
        float low = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            low = Mathf.Lerp(Random.Range(-1f, 1f), low, smooth);
            float thump = Mathf.Sin(2f * Mathf.PI * 110f * t) * Mathf.Exp(-t * 35f);
            data[i] = Mathf.Clamp((low * Mathf.Exp(-t * decay) + thump * 0.7f) * volume, -1f, 1f);
        }
        var clip = AudioClip.Create(name, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static AudioClip Chop(string name, float seconds, int beats)
    {
        const int rate = 44100;
        int n = Mathf.CeilToInt(rate * seconds);
        var data = new float[n];
        float low = 0f;
        for (int i = 0; i < n; i++)
        {
            float phase = Mathf.Repeat(i / (float)n * beats, 1f);
            low = Mathf.Lerp(Random.Range(-1f, 1f), low, 0.93f);
            float beat = Mathf.Exp(-phase * 5f);
            data[i] = Mathf.Clamp(low * (0.35f + 2.4f * beat) * 2.2f, -1f, 1f);
        }
        // Blend the ends so the loop has no click.
        const int fade = 400;
        for (int i = 0; i < fade; i++)
        {
            float k = i / (float)fade;
            data[i] = Mathf.Lerp(data[n - fade + i], data[i], k);
        }
        var clip = AudioClip.Create(name, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    public static void Play(AudioClip clip, float volume = 1f)
    {
        if (clip == null) return;
        if (s_source == null)
        {
            var go = new GameObject("ProceduralSfxSource");
            s_source = go.AddComponent<AudioSource>();
            s_source.playOnAwake = false;
            s_source.spatialBlend = 0f;
            s_source.ignoreListenerPause = true;
        }
        s_source.PlayOneShot(clip, volume * GameSettings.SfxVolume);
    }

    private static AudioClip Sweep(string name, float f0, float f1, float seconds, float volume, bool square)
    {
        const int rate = 44100;
        int n = Mathf.CeilToInt(rate * seconds);
        var data = new float[n];
        double phase = 0;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n;
            float f = Mathf.Lerp(f0, f1, t);
            phase += 2.0 * Mathf.PI * f / rate;
            float s = Mathf.Sin((float)phase);
            if (square) s = s >= 0f ? 0.6f : -0.6f;
            float env = Mathf.Min(1f, t * 20f) * (1f - t); // quick attack, linear release
            data[i] = s * env * volume;
        }
        var clip = AudioClip.Create(name, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static AudioClip TwoTone(string name, float fa, float fb, float seconds, float volume)
    {
        const int rate = 44100;
        int n = Mathf.CeilToInt(rate * seconds);
        var data = new float[n];
        double phase = 0;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n;
            float f = (int)(t * 4f) % 2 == 0 ? fa : fb;
            phase += 2.0 * Mathf.PI * f / rate;
            float env = Mathf.Min(1f, t * 30f) * Mathf.Min(1f, (1f - t) * 10f);
            data[i] = Mathf.Sin((float)phase) * env * volume;
        }
        var clip = AudioClip.Create(name, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
