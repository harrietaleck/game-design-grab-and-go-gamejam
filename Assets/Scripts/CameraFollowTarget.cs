using UnityEngine;

public class CameraFollowTarget : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Tooltip("Camera position = target.position + offset. Less negative Z = closer behind the runner (e.g. -5 is tighter than -12).")]
    public Vector3 offset = new Vector3(0f, 2.4f, -5f);

    [Header("Smoothing")]
    [Tooltip("Higher = snappier camera. 0 disables smoothing (teleports).")]
    public float followSmooth = 10f;

    [Header("Look At")]
    public bool lookAtTarget = true;
    public Vector3 lookAtOffset = new Vector3(0f, 1.2f, 0f);

    [Header("Hit Shake (Task 7.2)")]
    [Tooltip("Shake strength when Claire crashes. 0 disables it.")]
    public float hitShakeAmplitude = 0.12f;
    public float hitShakeDuration = 0.22f;

    private float _shakeUntil;
    private float _shakeAmplitude;

    private void OnEnable() => GameEvents.OnEvent += OnGameEvent;
    private void OnDisable() => GameEvents.OnEvent -= OnGameEvent;

    private void OnGameEvent(GameEventData e)
    {
        if (e.type == GameEventType.ClaireCollision)
            Shake(hitShakeAmplitude, hitShakeDuration);
    }

    /// <summary>Short, subtle positional shake (scaled time: stops when paused).</summary>
    public void Shake(float amplitude, float duration)
    {
        if (amplitude <= 0f || duration <= 0f) return;
        _shakeAmplitude = amplitude;
        _shakeUntil = Time.time + duration;
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        Vector3 desiredPos = target.position + offset;
        if (followSmooth <= 0f)
        {
            transform.position = desiredPos;
        }
        else
        {
            transform.position = Vector3.Lerp(transform.position, desiredPos, followSmooth * Time.deltaTime);
        }

        if (Time.time < _shakeUntil)
        {
            float k = (_shakeUntil - Time.time) / Mathf.Max(0.01f, hitShakeDuration);
            transform.position += Random.insideUnitSphere * (_shakeAmplitude * k);
        }

        if (lookAtTarget)
        {
            Vector3 lookAtPoint = target.position + lookAtOffset;
            Vector3 direction = lookAtPoint - transform.position;
            if (direction.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }
    }
}
