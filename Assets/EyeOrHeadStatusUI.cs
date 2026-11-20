using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class EyeOrHeadStatusUI : MonoBehaviour
{
    // Start is called before the first frame update
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI label;

    [Header("Input Actions")]
    [SerializeField] private InputActionReference eyeIsTracked;      // path: /<OpenXREyeGaze>/isTracked (Button)
    [SerializeField] private InputActionReference eyeTrackingState;  // path: /<OpenXREyeGaze>/trackingState (Value/Integer)

    [SerializeField, Tooltip("Małe opóźnienie, by uniknąć migotania przy krótkich dropach trackingu.")]
    private float debounce = 0.2f;

    private float lostSince = -1f;
    private bool eyeActive = false;

    void OnEnable()
    {
        if (eyeIsTracked) eyeIsTracked.action.Enable();
        if (eyeTrackingState) eyeTrackingState.action.Enable();
        UpdateLabel();
    }

    void OnDisable()
    {
        if (eyeIsTracked) eyeIsTracked.action.Disable();
        if (eyeTrackingState) eyeTrackingState.action.Disable();
    }

    void Update()
    {
        bool tracked = eyeIsTracked && eyeIsTracked.action.ReadValue<float>() > 0.5f;
        int state = eyeTrackingState ? eyeTrackingState.action.ReadValue<int>() : 0;

        bool hasPose = (state & (int)UnityEngine.XR.InputTrackingState.Rotation) != 0
                       || (state & (int)UnityEngine.XR.InputTrackingState.Position) != 0;

        bool nowActive = tracked && hasPose;

        if (nowActive)
        {
            lostSince = -1f;
            if (!eyeActive) { eyeActive = true; UpdateLabel(); }
        }
        else
        {
            if (lostSince < 0f) lostSince = Time.unscaledTime;
            if (!eyeActive && (Time.unscaledTime - lostSince) < debounce) return;
            if (eyeActive && (Time.unscaledTime - lostSince) >= debounce)
            {
                eyeActive = false; UpdateLabel();
            }
        }
    }

    private void UpdateLabel()
    {
        if (!label) return;
        if (eyeActive)
        {
            label.text = "👁️ Eye tracking: AKTYWNY";
            label.color = new Color(0.2f, 0.85f, 0.3f);
        }
        else
        {
            label.text = "↪️ Fallback: HEAD";
            label.color = new Color(1.0f, 0.75f, 0.25f);
        }
    }
}
