using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Sends AJ dialogue requests to the Grab &amp; Go dialogue backend (Task 4.9).
/// - Disabled unless StreamingAssets/dialogue_config.json says enabled with an endpoint.
/// - No API keys here: the backend holds the provider key. The optional clientToken only identifies the game build.
/// - Asynchronous (UnityWebRequest coroutine), one request at a time, hard timeout,
///   every failure reported as null so the controller falls back to prewritten lines.
/// </summary>
public class DialogueAPIClient : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        GameStartFlow.RegisterBootstrap(flow =>
        {
            if (flow.GetComponent<DialogueAPIClient>() == null)
                flow.gameObject.AddComponent<DialogueAPIClient>();
        });
    }

    [Serializable]
    public class Config
    {
        public bool enabled = false;
        public string endpoint = "http://127.0.0.1:8787/dialogue";
        public string clientToken = "";
        public int timeoutSeconds = 4;
        public bool verboseLogging = false;
    }

    [Serializable]
    private class Response
    {
        public string line;
    }

    public const string ConfigFileName = "dialogue_config.json";

    public Config ActiveConfig { get; private set; }
    public bool IsConfigured { get; private set; }
    public bool Busy => _inFlight != null;
    public int RequestsSent { get; private set; }
    public int RequestsFailed { get; private set; }

    private UnityWebRequest _inFlight;

    private void Awake()
    {
        ActiveConfig = LoadConfig();
        IsConfigured = ActiveConfig != null && ActiveConfig.enabled && Uri.TryCreate(ActiveConfig.endpoint, UriKind.Absolute, out var uri)
                       && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        if (IsConfigured)
        {
            AJDialogueController.RemoteProvider = Send;
            Log($"LLM dialogue enabled -> {ActiveConfig.endpoint}");
        }
        else
        {
            Log("LLM dialogue disabled (no/disabled config). Using prewritten lines.");
        }
    }

    private void OnDestroy()
    {
        if (AJDialogueController.RemoteProvider == (Func<DialogueRequest, Action<string>, bool>)Send)
            AJDialogueController.RemoteProvider = null;
        if (_inFlight != null)
        {
            _inFlight.Abort();
            _inFlight.Dispose();
            _inFlight = null;
        }
    }

    private static Config LoadConfig()
    {
        try
        {
            string path = Path.Combine(Application.streamingAssetsPath, ConfigFileName);
            if (!File.Exists(path)) return null;
            return JsonUtility.FromJson<Config>(File.ReadAllText(path));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[DialogueAPIClient] Could not read {ConfigFileName}: {e.Message}");
            return null;
        }
    }

    /// <summary>Returns false (caller uses a fallback line) if a request is already running or not configured.</summary>
    public bool Send(DialogueRequest request, Action<string> onDone)
    {
        if (!IsConfigured || Busy || request == null || !isActiveAndEnabled)
            return false;
        StartCoroutine(SendRoutine(request, onDone));
        return true;
    }

    private IEnumerator SendRoutine(DialogueRequest request, Action<string> onDone)
    {
        string json = JsonUtility.ToJson(request);
        var www = new UnityWebRequest(ActiveConfig.endpoint, UnityWebRequest.kHttpVerbPOST)
        {
            uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json)),
            downloadHandler = new DownloadHandlerBuffer(),
            timeout = Mathf.Clamp(ActiveConfig.timeoutSeconds, 1, 15)
        };
        www.SetRequestHeader("Content-Type", "application/json");
        if (!string.IsNullOrEmpty(ActiveConfig.clientToken))
            www.SetRequestHeader("X-Client-Token", ActiveConfig.clientToken);

        _inFlight = www;
        RequestsSent++;
        float started = Time.unscaledTime;
        yield return www.SendWebRequest();

        string line = null;
        if (_inFlight == www) // not aborted
        {
            if (www.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var parsed = JsonUtility.FromJson<Response>(www.downloadHandler.text);
                    line = parsed != null ? parsed.line : null;
                }
                catch (Exception e)
                {
                    Log($"Invalid response JSON: {e.Message}");
                }
            }
            else
            {
                Log($"Request failed ({www.responseCode}): {www.error}");
            }
            _inFlight = null;
        }

        if (string.IsNullOrEmpty(line)) RequestsFailed++;
        Log($"{request.@event} -> {(line ?? "<none>")} in {(Time.unscaledTime - started):0.00}s");
        www.Dispose();

        try { onDone?.Invoke(line); }
        catch (Exception e) { Debug.LogException(e); }
    }

    private void Log(string message)
    {
        if (ActiveConfig != null && ActiveConfig.verboseLogging || Debug.isDebugBuild && Application.isEditor)
            Debug.Log($"[DialogueAPIClient] {message}");
    }
}
