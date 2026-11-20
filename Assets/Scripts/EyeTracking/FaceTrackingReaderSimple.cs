using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.OpenXR;
using VIVE.OpenXR.FacialTracking;

public class FaceTrackingReader : MonoBehaviour
{
    private ViveFacialTracking faceFeature;
    float[] lips;


    void Start()
    {
        faceFeature = OpenXRSettings.Instance.GetFeature<ViveFacialTracking>();
        lips = new float[64];
    }


    void Update()
    {
        if (faceFeature != null && faceFeature.GetFacialExpressions(
        XrFacialTrackingTypeHTC.XR_FACIAL_TRACKING_TYPE_LIP_DEFAULT_HTC, out float[] lipData))
        {
            lips = lipData;
            Debug.Log("Mouth open: " + lips[(int)XrLipExpressionHTC.XR_LIP_EXPRESSION_JAW_OPEN_HTC]);
        }
    }
}
