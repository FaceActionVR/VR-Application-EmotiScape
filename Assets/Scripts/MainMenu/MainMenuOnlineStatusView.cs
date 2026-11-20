using System.Collections;
using Telemetry.Online;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuOnlineStatusView : MonoBehaviour
{
    private const string LogPrefix = "[MAIN_MENU_ONLINE_STATUS_VIEW]";

    [Header("References")]
    [SerializeField] private VrOnlineSessionController onlineSessionController;

    [Header("Buttons")]
    [SerializeField] private Button startOnlineButton;
    [SerializeField] private Button startOfflineButton;
    [SerializeField] private Button startCuriosityButton;

    [Header("Texts")]
    [SerializeField] private TMP_Text statusTitleText;
    [SerializeField] private TMP_Text statusBodyText;

    [Header("Optional Background")]
    [SerializeField] private Image statusBackground;

    [Header("Colors")]
    [SerializeField] private Color offlineColor = new Color(0.7f, 0.1f, 0.1f);
    [SerializeField] private Color standbyColor = new Color(0.85f, 0.55f, 0.1f);
    [SerializeField] private Color readyColor = new Color(0.1f, 0.55f, 0.2f);
    [SerializeField] private Color errorColor = new Color(0.8f, 0.1f, 0.1f);
    [SerializeField] private Color neutralColor = new Color(0.35f, 0.35f, 0.35f);

    [Header("Refresh")]
    [SerializeField] private float refreshIntervalSeconds = 0.25f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private Coroutine refreshCoroutine;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (onlineSessionController != null)
            onlineSessionController.OnStateChanged += RefreshNow;

        refreshCoroutine = StartCoroutine(RefreshLoop());
        RefreshNow();
    }

    private void OnDisable()
    {
        if (onlineSessionController != null)
            onlineSessionController.OnStateChanged -= RefreshNow;

        if (refreshCoroutine != null)
        {
            StopCoroutine(refreshCoroutine);
            refreshCoroutine = null;
        }
    }

    private void ResolveReferences()
    {
        if (onlineSessionController == null)
            onlineSessionController = FindObjectOfType<VrOnlineSessionController>();
    }
    private IEnumerator RefreshLoop()
    {
        while (true)
        {
            RefreshNow();
            yield return new WaitForSeconds(refreshIntervalSeconds);
        }
    }

    public void RefreshNow()
    {
        ResolveReferences();

        if (statusTitleText != null)
            statusTitleText.text = "Status serwera";

        if (onlineSessionController == null)
        {
            SetStatus(
                "backend: <color=#FF3333>controler?</color>\n" +
                "session: skip\n" +
                "user: skip\n" +
                "FaceTracking: <color=#AAAAAA>skip</color>\n" +
                "EyeTracking: <color=#AAAAAA>skip</color>",
                false,
                errorColor
            );

            return;
        }

        bool canStartOnline = onlineSessionController.CanStartOnline;

        string text =
            onlineSessionController.GetBackendLine() + "\n" +
            onlineSessionController.GetSessionLine() + "\n" +
            onlineSessionController.GetUserLine() + "\n" +
            "FaceTracking: <color=#AAAAAA>skip</color>\n" +
            "EyeTracking: <color=#AAAAAA>skip</color>";

        SetStatus(text, canStartOnline, ResolveColor());
    }


    private void SetStatus(string body, bool canStartOnline, Color backgroundColor)
    {
        if (statusBodyText != null)
            statusBodyText.text = body;

        if (startOnlineButton != null)
            startOnlineButton.interactable = canStartOnline;

        if (startOfflineButton != null)
            startOfflineButton.interactable = true;

        if (startCuriosityButton != null)
            startCuriosityButton.interactable = true;

        if (statusBackground != null)
            statusBackground.color = backgroundColor;
    }
    private Color ResolveColor()
    {
        if (onlineSessionController == null)
            return errorColor;

        if (onlineSessionController.ServerLiveConfirmed)
        {
            switch (onlineSessionController.State)
            {
                case VrOnlineState.SessionQueued:
                    return readyColor;

                case VrOnlineState.MenuStandby:
                    return standbyColor;

                case VrOnlineState.Starting:
                    return standbyColor;

                case VrOnlineState.Running:
                    return readyColor;

                case VrOnlineState.AuthOrStatusError:
                case VrOnlineState.Failed:
                    return errorColor;

                default:
                    return readyColor;
            }
        }

        switch (onlineSessionController.State)
        {
            case VrOnlineState.CheckingServer:
                return standbyColor;

            case VrOnlineState.ServerOffline:
            case VrOnlineState.AuthOrStatusError:
            case VrOnlineState.Failed:
                return errorColor;

            default:
                return neutralColor;
        }
    }
    private void Log(string message)
    {
        if (!debugLogs)
            return;

        Debug.Log($"{LogPrefix} {message}");
    }
}