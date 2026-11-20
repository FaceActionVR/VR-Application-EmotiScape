using Telemetry.ExperimentFlow;
using Telemetry.Online;
using UnityEngine;

public class EndExperimentButton : MonoBehaviour
{
    private const string LogPrefix = "[END_EXPERIMENT_BUTTON]";

    [Header("References")]
    [SerializeField] private EndFlowController endFlowController;
    [SerializeField] private VrOnlineSessionController onlineSessionController;

    [Header("Mode")]
    [Tooltip("Je¿eli true, przycisk najpierw próbuje zakoñczyæ sesjê online przez backendSessionId.")]
    [SerializeField] private bool preferOnlineEndFlow = true;

    [Header("Debug")]
    [SerializeField] private bool isDebug = true;

    private void Awake()
    {
        ResolveReferences();
    }

    private void ResolveReferences()
    {
        if (endFlowController == null)
            endFlowController = FindObjectOfType<EndFlowController>();

        if (onlineSessionController == null)
            onlineSessionController = FindObjectOfType<VrOnlineSessionController>();
    }

    public void EndExperiment()
    {
        ResolveReferences();

        if (endFlowController == null)
        {
            LogWarning("EndFlowController not found.");
            return;
        }

        if (preferOnlineEndFlow &&
            onlineSessionController != null &&
            onlineSessionController.BackendSessionId > 0)
        {
            int backendSessionId = onlineSessionController.BackendSessionId;

            Log($"Online end experiment button clicked. backendSessionId={backendSessionId}");

            endFlowController.EndOnlineExperimentAndReturnToMenu(backendSessionId);
            return;
        }

        Log("Local/offline end experiment button clicked.");

        endFlowController.EndExperimentAndReturnToMenu();
    }

    private void Log(string message)
    {
        if (!isDebug)
            return;

        Debug.Log($"{LogPrefix} {message}");
    }

    private void LogWarning(string message)
    {
        if (!isDebug)
            return;

        Debug.LogWarning($"{LogPrefix} {message}");
    }
}