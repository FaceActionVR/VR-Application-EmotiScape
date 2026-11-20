using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VignetteEnableOnMotion : MonoBehaviour
{
    [Header("Refs")]
    public GameObject tunnelingVignette;   // ca³y obiekt z Mesh Rendererem + Controllerem

    [Header("Progi w³¹czenia")]
    public float minLinearSpeed = 0.20f;   // m/s, ruch translacyjny
    public float minAngularSpeed = 12f;    // deg/s, obrót g³owy

    [Header("Histereza (migniêcia)")]
    public float debounceSeconds = 0.25f;

    private Transform cam;
    private Vector3 lastPos;
    private Quaternion lastRot;
    private float stillSince = 0f;
    private bool isOn = false;

    void Start()
    {
        cam = Camera.main ? Camera.main.transform : null;
        if (!tunnelingVignette) tunnelingVignette = gameObject;
        if (cam != null)
        {
            lastPos = cam.position;
            lastRot = cam.rotation;
        }
        SetOn(false); // start: wy³¹czone
    }

    void Update()
    {
        if (cam == null) return;

        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        float lin = (cam.position - lastPos).magnitude / dt;
        float ang = Quaternion.Angle(lastRot, cam.rotation) / dt;

        bool moving = lin >= minLinearSpeed || ang >= minAngularSpeed;

        if (moving)
        {
            stillSince = 0f;
            if (!isOn) SetOn(true);
        }
        else
        {
            if (stillSince == 0f) stillSince = Time.unscaledTime;
            if (isOn && (Time.unscaledTime - stillSince) >= debounceSeconds)
                SetOn(false);
        }

        lastPos = cam.position;
        lastRot = cam.rotation;
    }

    private void SetOn(bool on)
    {
        isOn = on;
        if (tunnelingVignette && tunnelingVignette.activeSelf != on)
            tunnelingVignette.SetActive(on);
    }
}
