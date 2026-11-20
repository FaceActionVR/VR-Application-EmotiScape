using Telemetry.Session;
using UnityEngine;
using UnityEngine.InputSystem;

public class TelemetryDebugController : MonoBehaviour
{
    [SerializeField] private string testSessionId = "TEST_SESSION_001";
    [SerializeField] private string testUserId = "EDITOR_USER";

    void Update()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.sKey.wasPressedThisFrame)
        {
            TelemetrySessionManager.Instance.StartSession(testSessionId, testUserId);
        }

        if (Keyboard.current.xKey.wasPressedThisFrame)
        {
            TelemetrySessionManager.Instance.StopSession();
        }

        if (Keyboard.current.tKey.wasPressedThisFrame)
        {
            Debug.Log($"Session time: {SessionClock.Instance.CurrentTimeSeconds:F3}s");
        }
    }
}