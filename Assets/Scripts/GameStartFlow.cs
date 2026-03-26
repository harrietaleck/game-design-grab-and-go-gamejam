using UnityEngine;
using UnityEngine.UI;

public class GameStartFlow : MonoBehaviour
{
    [Header("UI")]
    public GameObject menuPanel;
    public GameObject hudPanel;
    public Button playButton;
    public HUDController hudController;

    [Header("Gameplay Components")]
    [Tooltip("Optional scripts/components to enable only after Play is clicked.")]
    public Behaviour[] enableOnStart;

    [Tooltip("Pause time before Play is clicked.")]
    public bool pauseTimeUntilStart = true;

    private bool _started;

    private void Awake()
    {
        if (playButton != null)
            playButton.onClick.AddListener(StartGame);

        if (menuPanel != null)
            menuPanel.SetActive(true);
        if (hudPanel != null)
            hudPanel.SetActive(false);

        if (hudController != null)
            hudController.EndRun("READY");

        SetGameplayEnabled(false);

        if (pauseTimeUntilStart)
            Time.timeScale = 0f;
    }

    public void StartGame()
    {
        if (_started)
            return;

        _started = true;

        if (pauseTimeUntilStart)
            Time.timeScale = 1f;

        SetGameplayEnabled(true);

        if (menuPanel != null)
            menuPanel.SetActive(false);
        if (hudPanel != null)
            hudPanel.SetActive(true);
        if (hudController != null)
            hudController.BeginRun();
    }

    private void SetGameplayEnabled(bool enabled)
    {
        if (enableOnStart == null)
            return;

        for (int i = 0; i < enableOnStart.Length; i++)
        {
            if (enableOnStart[i] != null)
                enableOnStart[i].enabled = enabled;
        }
    }
}
