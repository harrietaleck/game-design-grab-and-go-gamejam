using UnityEngine;

public class CollectibleSpin : MonoBehaviour
{
    public Vector3 axis = Vector3.up;
    public float speedDegreesPerSecond = 240f;

    private void Update()
    {
        if (axis.sqrMagnitude <= 0.0001f || Mathf.Approximately(speedDegreesPerSecond, 0f))
            return;

        transform.Rotate(axis.normalized, speedDegreesPerSecond * Time.deltaTime, Space.Self);
    }
}
