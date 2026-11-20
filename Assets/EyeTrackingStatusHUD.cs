using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR;
using XRCommonUsages = UnityEngine.XR.CommonUsages;
using XRInputDevice = UnityEngine.XR.InputDevice;

public class EyeTrackingStatusHUD : MonoBehaviour
{
    [Header("Sprites")]
    public Sprite eyeBlack;        // neutralne / nieaktywny
    public Sprite eyeGreen;        // aktywny
    public Sprite eyeYellow;       // chwilowy fallback
    public Sprite eyeRed;          // wyłączony / błąd
    public Sprite eyeCrossRed;     // przekreślone

    [Header("Input Actions (OpenXR)")]
    [Tooltip("Action Reference do /<OpenXREyeGaze>/isTracked")]
    public InputActionReference eyeIsTracked;
    [Tooltip("Action Reference do /<OpenXREyeGaze>/trackingState")]
    public InputActionReference eyeTrackingState;

    [Header("Ustawienia logiki")]
    [Tooltip("Czas w sekundach po którym zmienia się stan na fallback")]
    public float lostDelay = 0.3f;

    private Image img;
    private float lostSince = -1f;
    private bool lastTracked = false;

    // --- nowa sekcja logowania ---
    private const string TAG = "[EyeTrackingHUD]";
    private EyeState currentState = EyeState.Neutral;

    void Awake()
    {
        img = GetComponent<Image>();
    }

    void OnEnable()
    {
        eyeIsTracked?.action.Enable();
        eyeTrackingState?.action.Enable();
        SetState(EyeState.Neutral);
        lastTracked = false;
        lostSince = -1f;
        Debug.Log($"{TAG} OnEnable() – initialized");
    }

    void OnDisable()
    {
        eyeIsTracked?.action.Disable();
        eyeTrackingState?.action.Disable();
        Debug.Log($"{TAG} OnDisable()");
    }

    void Update()
    {
        bool xrEyeTracked = false;
        var eyeDevices = tempList ?? (tempList = tempList = new List<XRInputDevice>());
        eyeDevices.Clear();
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.EyeTracking, eyeDevices);

        if (eyeDevices.Count > 0)
        {
            XRInputDevice dev = eyeDevices[0];
            if (dev.isValid && dev.TryGetFeatureValue(XRCommonUsages.isTracked, out bool trackedFlag))
                xrEyeTracked = trackedFlag;
        }
        else
        {
            // 2) Fallback: CenterEye (często zwraca stan śledzenia oczu na części HMD)
            XRInputDevice center = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.CenterEye);
            if (center.isValid && center.TryGetFeatureValue(XRCommonUsages.isTracked, out bool centerTracked))
                xrEyeTracked = centerTracked;
        }

        // 3) (opcjonalnie) plan B: Input Actions – jeśli masz je podpięte
        bool iaTracked = eyeIsTracked && eyeIsTracked.action.ReadValue<float>() > 0.5f;
        int iaState = eyeTrackingState ? eyeTrackingState.action.ReadValue<int>() : 0;
        bool iaHasRotation = (iaState & (int)UnityEngine.XR.InputTrackingState.Rotation) != 0;

        // Log co 60 klatek
        if (Time.frameCount % 60 == 0)
            Debug.Log($"{TAG} F={Time.frameCount} | xrEyeTracked={xrEyeTracked} | iaTracked={iaTracked} | iaState={iaState} | iaHasRot={iaHasRotation} | currentState={currentState}");

        bool eyeActiveNow = xrEyeTracked || iaTracked || iaHasRotation;

        if (eyeActiveNow)
        {
            lostSince = -1f;
            if (!lastTracked)
            {
                SetState(EyeState.Active);
                Debug.Log($"{TAG} → Eye ACTIVE");
            }
            lastTracked = true;
        }
        else
        {
            if (lostSince < 0f) lostSince = Time.unscaledTime;
            if (Time.unscaledTime - lostSince > lostDelay)
            {
                SetState(lastTracked ? EyeState.Fallback : EyeState.Lost);
                Debug.Log($"{TAG} → {(lastTracked ? "Fallback" : "LOST")}");
                lastTracked = false;
            }
        }

        // podtrzymanie Enable() akcji co 60 klatek (opcjonalnie)
        if (!Application.isEditor && Time.frameCount % 60 == 0)
        {
            eyeIsTracked?.action.Enable();
            eyeTrackingState?.action.Enable();
        }
    }

    private static List<XRInputDevice> tempList;

    private void SetState(EyeState state)
    {
        if (!img) return;
        currentState = state;

        switch (state)
        {
            case EyeState.Active:
                img.sprite = eyeGreen;
                img.color = Color.white;
                break;
            case EyeState.Fallback:
                img.sprite = eyeYellow;
                img.color = Color.white;
                break;
            case EyeState.Lost:
                img.sprite = eyeCrossRed ? eyeCrossRed : eyeRed;
                img.color = Color.white;
                break;
            default:
                img.sprite = eyeBlack;
                img.color = new Color(1f, 1f, 1f, 0.7f);
                break;
        }
    }

    private enum EyeState { Neutral, Active, Fallback, Lost }
}
