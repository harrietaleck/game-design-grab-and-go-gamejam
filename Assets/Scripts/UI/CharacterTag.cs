using TMPro;
using UnityEngine;

/// <summary>
/// Floating "AJ" tag above the thief (Task 7.3) so he stays recognisable ahead of Claire.
/// Turns red and pulses while he is caught.
/// </summary>
public class CharacterTag : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        GameStartFlow.RegisterBootstrap(flow =>
        {
            var aj = FindFirstObjectByType<AIThiefLaneDodge>();
            if (aj != null && FindFirstObjectByType<CharacterTag>() == null)
            {
                // Not parented: the character model may be scaled, which would shrink the text.
                var go = new GameObject("AJ Tag (runtime)");
                go.AddComponent<CharacterTag>().Init(aj, "AJ");
            }
        });
    }

    public float height = 2.4f;
    private AIThiefLaneDodge _aj;
    private TextMeshPro _text;

    public void Init(AIThiefLaneDodge aj, string label)
    {
        _aj = aj;
        _text = gameObject.AddComponent<TextMeshPro>();
        _text.text = label;
        _text.fontSize = 5f;
        _text.alignment = TextAlignmentOptions.Center;
        _text.fontStyle = FontStyles.Bold;
        _text.color = RuntimeUI.Accent;
        transform.position = aj.transform.position + Vector3.up * height;
    }

    private void LateUpdate()
    {
        if (_text == null) return;
        if (_aj == null) { Destroy(gameObject); return; }
        transform.position = _aj.transform.position + Vector3.up * height;
        var cam = Camera.main;
        if (cam != null) transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
        bool caught = _aj != null && _aj.IsCaught;
        _text.text = caught ? "AJ!" : "AJ";
        _text.color = caught
            ? Color.Lerp(RuntimeUI.Danger, Color.white, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 10f))
            : RuntimeUI.Accent;
    }
}
