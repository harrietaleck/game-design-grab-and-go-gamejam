using UnityEngine;

/// <summary>Slides a HUD element in from the screen edge whenever it appears, keeping pop-ups off the lanes.</summary>
public class SidePopup : MonoBehaviour
{
    private const float SlideSeconds = 0.28f;

    private RectTransform _rect;
    private Vector2 _target;
    private float _direction;
    private float _startedAt = float.MinValue;

    public static SidePopup Attach(RectTransform rect, bool fromRight)
    {
        var popup = rect.gameObject.AddComponent<SidePopup>();
        popup._rect = rect;
        popup._target = rect.anchoredPosition;
        popup._direction = fromRight ? 1f : -1f;
        return popup;
    }

    /// <summary>Restarts the slide (for elements that stay active and only change their text).</summary>
    public void Play()
    {
        _startedAt = Time.unscaledTime;
        Apply(0f);
    }

    private void OnEnable()
    {
        if (_rect != null) Play();
    }

    private void OnDisable()
    {
        if (_rect != null) _rect.anchoredPosition = _target;
    }

    private void Update()
    {
        float t = Mathf.Clamp01((Time.unscaledTime - _startedAt) / SlideSeconds);
        Apply(1f - (1f - t) * (1f - t) * (1f - t));
    }

    private void Apply(float progress)
    {
        float offscreen = _rect.rect.width + 40f;
        _rect.anchoredPosition = _target + new Vector2(_direction * offscreen * (1f - progress), 0f);
    }
}
