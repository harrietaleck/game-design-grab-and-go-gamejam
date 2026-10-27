using TMPro;
using UnityEngine;

/// <summary>Floating "+1" / "+5" world-space text that rises and fades. Created per pickup, self-destroys.</summary>
public class LootPopup : MonoBehaviour
{
    private TextMeshPro _text;
    private float _born;
    private const float Life = 0.8f;
    private Color _color;

    public static void Spawn(Vector3 position, int value)
    {
        var go = new GameObject("LootPopup");
        go.transform.position = position + Vector3.up * 1.2f;
        var popup = go.AddComponent<LootPopup>();
        popup._text = go.AddComponent<TextMeshPro>();
        popup._text.text = value >= 5 ? $"+{value}" : $"+{value}";
        popup._text.fontSize = value >= 5 ? 9f : 6f;
        popup._text.alignment = TextAlignmentOptions.Center;
        popup._text.fontStyle = FontStyles.Bold;
        popup._color = value >= 5 ? new Color(1f, 0.8f, 0.2f) : new Color(1f, 0.95f, 0.6f);
        popup._text.color = popup._color;
        popup._born = Time.time;
    }

    private void LateUpdate()
    {
        float t = (Time.time - _born) / Life;
        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }
        transform.position += Vector3.up * (1.5f * Time.deltaTime);
        var cam = Camera.main;
        if (cam != null)
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
        var c = _color; c.a = 1f - t * t;
        _text.color = c;
    }
}
