using UnityEngine;

/// <summary>
/// Tiny generated sound effects (no new audio assets): radio blip, distraction whoosh, alarm beep.
/// Clips are created once and cached.
/// </summary>
public static class ProceduralSfx
{
    private static AudioClip s_blip, s_pulse, s_alarm, s_rescue;
    private static AudioSource s_source;

    public static AudioClip RadioBlip => s_blip ??= Sweep("RadioBlip", 1400f, 1900f, 0.07f, 0.35f, square: true);
    public static AudioClip DistractionPulse => s_pulse ??= Sweep("DistractionPulse", 900f, 180f, 0.45f, 0.5f, square: false);
    public static AudioClip Alarm => s_alarm ??= TwoTone("Alarm", 880f, 660f, 0.6f, 0.35f);
    public static AudioClip Rescue => s_rescue ??= Sweep("Rescue", 400f, 1200f, 0.3f, 0.45f, square: false);

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
        s_source.PlayOneShot(clip, volume);
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
