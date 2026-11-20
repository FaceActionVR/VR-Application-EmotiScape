using System.Collections;
using Telemetry.SceneState;
using Telemetry.Session;
using Telemetry.Writers;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RoomTransitionEventLogger : MonoBehaviour
{
    private const string LogPrefix = "[ROOM_TRANSITION]";

    [SerializeField] private EventLogCsvWriter eventsWriter;
    [SerializeField] private bool debugLogs = true;

    private int previousRoomId = -1;
    private string previousSceneName = "";

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void ResolveReferences()
    {
        if (eventsWriter == null)
            eventsWriter = FindObjectOfType<EventLogCsvWriter>();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartCoroutine(LogSceneTransitionNextFrame(scene));
    }

    private IEnumerator LogSceneTransitionNextFrame(Scene scene)
    {
        yield return null;

        TelemetrySessionManager manager = TelemetrySessionManager.Instance;

        if (manager == null ||
            manager.Clock == null ||
            !manager.Clock.IsRunning)
        {
            yield break;
        }

        ResolveReferences();

        if (eventsWriter == null)
        {
            Debug.LogWarning(
                $"{LogPrefix} EventLogCsvWriter not found.");
            yield break;
        }

        RoomStateProvider roomStateProvider =
            FindObjectOfType<RoomStateProvider>();

        if (roomStateProvider == null)
        {
            Debug.LogWarning(
                $"{LogPrefix} RoomStateProvider not found in scene={scene.name}");
            yield break;
        }

        int newRoomId = roomStateProvider.CurrentRoomId;
        string newSceneName = scene.name;

        if (newRoomId == previousRoomId &&
            newSceneName == previousSceneName)
        {
            yield break;
        }

        double timestamp = manager.Clock.CurrentTimeSeconds;

        string extraJson =
            "{" +
            $"\"from_room_id\":{previousRoomId}," +
            $"\"to_room_id\":{newRoomId}," +
            $"\"from_scene\":\"{EscapeJson(previousSceneName)}\"," +
            $"\"to_scene\":\"{EscapeJson(newSceneName)}\"," +
            "\"source\":\"scene_change\"," +
            "\"forced\":false" +
            "}";

        eventsWriter.LogEvent(
            timestamp,
            timestamp,
            "ROOM_TRANSITION",
            newSceneName,
            0.0,
            newRoomId,
            extraJson
        );

        if (debugLogs)
        {
            Debug.Log(
                $"{LogPrefix} {previousRoomId}/{previousSceneName} -> " +
                $"{newRoomId}/{newSceneName} at {timestamp:F3}s");
        }

        previousRoomId = newRoomId;
        previousSceneName = newSceneName;
    }

    private string EscapeJson(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"");
    }
}