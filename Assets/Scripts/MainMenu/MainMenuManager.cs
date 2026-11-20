using Telemetry.Online;
using Telemetry.Session;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    private const string LogPrefix = "[MAIN_MENU]";

    [Header("Scene Names")]
    [SerializeField] private string experimentSceneName = "SampleScene";
    [SerializeField] private string secondSceneName = "CuriosityScene";

    [Header("Online")]
    [SerializeField] private VrOnlineSessionController onlineSessionController;

    [Header("Offline")]
    [Tooltip("Sta³e ID sesji offline. Bez daty, ¿eby kolejne testy offline nadpisywa³y dane.")]
    [SerializeField] private string offlineSessionId = "OFFLINE_TEST";

    [Tooltip("Sta³y user offline. Najlepiej taki sam jak sessionId dla testów technicznych.")]
    [SerializeField] private string offlineUserId = "OFFLINE_TEST";

    [Header("Curiosity / Local Test")]
    [SerializeField] private string curiosityUserId = "DEBUG_USER";

    private void Awake()
    {
        ResolveReferences();
    }

    private void ResolveReferences()
    {
        if (onlineSessionController == null)
            onlineSessionController = FindObjectOfType<VrOnlineSessionController>();
    }

    // Podepnij do przycisku START ONLINE
    public void StartOnlineExperiment()
    {
        ResolveReferences();

        if (onlineSessionController == null)
        {
            Debug.LogWarning($"{LogPrefix} Cannot start online. VrOnlineSessionController not found.");
            return;
        }

        if (!onlineSessionController.CanStartOnline)
        {
            Debug.LogWarning(
                $"{LogPrefix} Cannot start online. " +
                $"CanStartOnline=false, state={onlineSessionController.State}, " +
                $"backendSessionId={onlineSessionController.BackendSessionId}");

            return;
        }

        Debug.Log(
            $"{LogPrefix} Starting ONLINE experiment. " +
            $"backendSessionId={onlineSessionController.BackendSessionId}, " +
            $"user={onlineSessionController.BackendUserIdentifier}");

        onlineSessionController.StartOnlineFromButton();
    }

    // Podepnij do przycisku START OFFLINE
    public void StartOfflineExperiment()
    {
        string safeSessionId = string.IsNullOrWhiteSpace(offlineSessionId)
            ? "OFFLINE_TEST"
            : offlineSessionId.Trim();

        string safeUserId = string.IsNullOrWhiteSpace(offlineUserId)
            ? safeSessionId
            : offlineUserId.Trim();

        Debug.Log(
            $"{LogPrefix} Starting OFFLINE experiment. " +
            $"sessionId={safeSessionId}, userId={safeUserId}");

        TelemetrySessionManager.Instance?.StartSession(safeSessionId, safeUserId);

        Debug.Log($"{LogPrefix} Loading offline experiment scene: {experimentSceneName}");
        SceneManager.LoadScene(experimentSceneName);
    }

    public void QuitApplication()
    {
        Debug.Log($"{LogPrefix} Quitting application.");
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}