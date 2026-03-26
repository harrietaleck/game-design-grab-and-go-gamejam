using UnityEngine;

public class AudioDirector : MonoBehaviour
{
    [Header("Audio Sources")]
    [Tooltip("Source used for ambience one-shots.")]
    public AudioSource ambienceSource;

    [Tooltip("Source used for gameplay/cue one-shots.")]
    public AudioSource gameplaySource;

    [Header("Ambience")]
    public AudioClip[] ambienceClips;
    [Range(0f, 1f)] public float ambienceVolumeMin = 0.15f;
    [Range(0f, 1f)] public float ambienceVolumeMax = 0.35f;
    public bool playAmbienceRandom = true;

    [Header("Gameplay Cues")]
    public AudioClip[] gameplayClips;
    [Range(0f, 1f)] public float gameplayVolumeMin = 0.35f;
    [Range(0f, 1f)] public float gameplayVolumeMax = 0.75f;
    public float gameplayIntervalMin = 12f;
    public float gameplayIntervalMax = 25f;
    public bool playGameplayRandom = true;

    [Header("Variation")]
    [Tooltip("Random pitch range for both categories.")]
    public float pitchMin = 0.95f;
    public float pitchMax = 1.05f;

    private float _nextGameplayAt;
    private int _lastAmbienceIndex = -1;
    private int _lastGameplayIndex = -1;

    private void Awake()
    {
        EnsureAudioSources();
        ScheduleNextGameplay();

        // Ambience should start immediately when the game starts.
        if (playAmbienceRandom)
            PlayRandomAmbience();
    }

    private void Update()
    {
        // Keep ambience continuous with no random start delay/gaps.
        if (playAmbienceRandom && ambienceSource != null && !ambienceSource.isPlaying)
        {
            PlayRandomAmbience();
        }

        if (playGameplayRandom && Time.time >= _nextGameplayAt)
        {
            PlayRandomGameplayCue();
            ScheduleNextGameplay();
        }
    }

    public void PlayRandomAmbience()
    {
        if (ambienceSource == null || ambienceClips == null || ambienceClips.Length == 0)
            return;

        int index = PickRandomIndexNoRepeat(ambienceClips.Length, _lastAmbienceIndex);
        var clip = ambienceClips[index];
        if (clip == null)
            return;

        _lastAmbienceIndex = index;
        ambienceSource.pitch = Random.Range(Mathf.Min(pitchMin, pitchMax), Mathf.Max(pitchMin, pitchMax));
        ambienceSource.clip = clip;
        ambienceSource.volume = Random.Range(Mathf.Min(ambienceVolumeMin, ambienceVolumeMax), Mathf.Max(ambienceVolumeMin, ambienceVolumeMax));
        ambienceSource.Play();
    }

    public void PlayRandomGameplayCue()
    {
        if (gameplaySource == null || gameplayClips == null || gameplayClips.Length == 0)
            return;

        int index = PickRandomIndexNoRepeat(gameplayClips.Length, _lastGameplayIndex);
        var clip = gameplayClips[index];
        if (clip == null)
            return;

        _lastGameplayIndex = index;
        PlayOneShot(gameplaySource, clip, gameplayVolumeMin, gameplayVolumeMax);
    }

    public void PlayGameplayCueByIndex(int index)
    {
        if (gameplaySource == null || gameplayClips == null || gameplayClips.Length == 0)
            return;

        if (index < 0 || index >= gameplayClips.Length)
            return;

        var clip = gameplayClips[index];
        if (clip == null)
            return;

        _lastGameplayIndex = index;
        PlayOneShot(gameplaySource, clip, gameplayVolumeMin, gameplayVolumeMax);
    }

    private void PlayOneShot(AudioSource source, AudioClip clip, float minVolume, float maxVolume)
    {
        float volume = Random.Range(Mathf.Min(minVolume, maxVolume), Mathf.Max(minVolume, maxVolume));
        source.pitch = Random.Range(Mathf.Min(pitchMin, pitchMax), Mathf.Max(pitchMin, pitchMax));
        source.PlayOneShot(clip, volume);
    }

    private void EnsureAudioSources()
    {
        if (ambienceSource == null)
        {
            var go = new GameObject("AmbienceAudioSource");
            go.transform.SetParent(transform, false);
            ambienceSource = go.AddComponent<AudioSource>();
            ambienceSource.playOnAwake = false;
            ambienceSource.loop = false;
            ambienceSource.spatialBlend = 0f;
        }

        if (gameplaySource == null)
        {
            var go = new GameObject("GameplayAudioSource");
            go.transform.SetParent(transform, false);
            gameplaySource = go.AddComponent<AudioSource>();
            gameplaySource.playOnAwake = false;
            gameplaySource.loop = false;
            gameplaySource.spatialBlend = 0f;
        }
    }

    private void ScheduleNextGameplay()
    {
        float min = Mathf.Max(0.1f, gameplayIntervalMin);
        float max = Mathf.Max(min, gameplayIntervalMax);
        _nextGameplayAt = Time.time + Random.Range(min, max);
    }

    private static int PickRandomIndexNoRepeat(int count, int previous)
    {
        if (count <= 1)
            return 0;

        int candidate = Random.Range(0, count - 1);
        if (candidate >= previous)
            candidate++;
        return candidate;
    }
}
