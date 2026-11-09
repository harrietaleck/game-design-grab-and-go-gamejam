using UnityEngine;

/// <summary>Short bursts from the barking recording: when a K9 joins the chase, and louder when one bites.</summary>
public class DogBarks : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        GameStartFlow.RegisterBootstrap(flow =>
        {
            if (flow.GetComponent<DogBarks>() == null)
                flow.gameObject.AddComponent<DogBarks>();
        });
    }

    private const float ChaseSeconds = 1.8f;
    private const float ChaseVolume = 0.6f;
    private const float BiteSeconds = 1.2f;
    private const float BiteVolume = 1f;
    private const float FadeSeconds = 0.25f;

    private static DogBarks s_instance;
    private AudioSource _source;
    private float _stopAt, _volume;

    /// <summary>A K9 has started chasing. Does not interrupt barking already under way.</summary>
    public static void ChaseBark()
    {
        if (s_instance != null && !s_instance._source.isPlaying) s_instance.Bark(ChaseSeconds, ChaseVolume);
    }

    public static void BiteBark()
    {
        if (s_instance != null) s_instance.Bark(BiteSeconds, BiteVolume);
    }

    private void Awake()
    {
        s_instance = this;
        _source = gameObject.AddComponent<AudioSource>();
        _source.clip = Resources.Load<AudioClip>("several-dogs-barking");
        _source.playOnAwake = false;
        _source.loop = true;
        _source.spatialBlend = 0f;
    }

    private void OnDestroy()
    {
        if (s_instance == this) s_instance = null;
    }

    private void Bark(float seconds, float volume)
    {
        if (_source.clip == null) return;
        _volume = volume;
        _stopAt = Time.unscaledTime + seconds;
        _source.volume = volume * GameSettings.SfxVolume;
        if (_source.isPlaying) return;
        // Starts somewhere different each time, so repeated bursts don't sound identical.
        _source.time = Random.Range(0f, Mathf.Max(0f, _source.clip.length - seconds));
        _source.Play();
    }

    private void Update()
    {
        if (!_source.isPlaying) return;
        float remaining = _stopAt - Time.unscaledTime;
        if (remaining <= 0f || !GameStartFlow.IsGameplayActive)
        {
            _source.Stop();
            return;
        }
        _source.volume = _volume * GameSettings.SfxVolume * Mathf.Clamp01(remaining / FadeSeconds);
    }
}
