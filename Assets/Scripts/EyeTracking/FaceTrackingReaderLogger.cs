using UnityEngine;
using UnityEngine.XR.OpenXR;
using VIVE.OpenXR.FacialTracking;

public class FaceTrackingReaderLogger : MonoBehaviour
{
    // G³ówny feature VIVE OpenXR odpowiedzialny za odczyt ekspresji
    private ViveFacialTracking faceFeature;

    // Bufory na dane ekspresji ust i oczu (rozmiary wynikaj¹ z definicji enumów SDK)
    private float[] lips = new float[(int)XrLipExpressionHTC.XR_LIP_EXPRESSION_MAX_ENUM_HTC];
    private float[] eyes = new float[(int)XrEyeExpressionHTC.XR_EYE_EXPRESSION_MAX_ENUM_HTC];

    private bool loggedMissingFeature;
    private bool loggedLipError;
    private bool loggedEyeError;

    private void Start()
    {
        // Uwaga: ten projekt u¿ywa w³asnego œrodowiska (stanowisko standalone na Vive Elite XR)
        // i mo¿e ró¿niæ siê od przyk³adowych skryptów z katalogu Wzroce.
        faceFeature = OpenXRSettings.Instance.GetFeature<ViveFacialTracking>();
        if (faceFeature == null || !faceFeature.enabled)
        {
            loggedMissingFeature = true;
            Debug.LogError("[FaceTrackingReader] Nie wykryto feature ViveFacialTracking. SprawdŸ: VIVE OpenXR 2.5.1, w³¹czone facial tracking w ustawieniach OpenXR i tryb standalone na goglach.");
        }
    }


    void Update()
    {
        if (faceFeature == null || !faceFeature.enabled)
        {
            return;
        }

        // Próba pobrania ekspresji ust
        if (faceFeature.GetFacialExpressions(XrFacialTrackingTypeHTC.XR_FACIAL_TRACKING_TYPE_LIP_DEFAULT_HTC, out float[] lipData))
        {
            lips = lipData;
            LogLipSnapshot();
        }
        else if (!loggedLipError)
        {
            loggedLipError = true;
            Debug.LogError("[FaceTrackingReader] Lip expressions nie s¹ dostêpne. Rozwi¹zanie: upewnij siê, ¿e modu³ facial tracker jest pod³¹czony oraz feature 'Lip Tracking' jest w³¹czony w ustawieniach OpenXR na goglach.");
        }

        // Próba pobrania ekspresji oczu (mo¿e nie byæ aktywna na ka¿dym urz¹dzeniu/konfiguracji)
        if (faceFeature.GetFacialExpressions(XrFacialTrackingTypeHTC.XR_FACIAL_TRACKING_TYPE_EYE_DEFAULT_HTC, out float[] eyeData))
        {
            eyes = eyeData;
            LogEyeSnapshot();
        }
        else if (!loggedEyeError)
        {
            loggedEyeError = true;
            Debug.LogError("[FaceTrackingReader] Eye expressions nie s¹ dostêpne. Rozwi¹zanie: sprawdŸ, czy w³¹czono Eye Tracking w ustawieniach OpenXR i czy urz¹dzenie je wspiera.");
        }
    }

    // Proste logowanie kilku najwa¿niejszych wartoœci w celu diagnostyki dzia³ania na docelowym projekcie
    private void LogLipSnapshot()
    {
        Debug.Log(
            "[FaceTrackingReader] Mouth open: " + lips[(int)XrLipExpressionHTC.XR_LIP_EXPRESSION_JAW_OPEN_HTC].ToString("F3") +
            ", Smile (Right/Left): " +
            lips[(int)XrLipExpressionHTC.XR_LIP_EXPRESSION_MOUTH_UPPER_UPRIGHT_HTC].ToString("F3") + "/" +
            lips[(int)XrLipExpressionHTC.XR_LIP_EXPRESSION_MOUTH_UPPER_UPLEFT_HTC].ToString("F3"));
    }

    private void LogEyeSnapshot()
    {
        Debug.Log(
            "[FaceTrackingReader] Eye blink (L/R): " +
            eyes[(int)XrEyeExpressionHTC.XR_EYE_EXPRESSION_LEFT_BLINK_HTC].ToString("F3") + "/" +
            eyes[(int)XrEyeExpressionHTC.XR_EYE_EXPRESSION_RIGHT_BLINK_HTC].ToString("F3") +
            ", Look up/down (L): " +
            eyes[(int)XrEyeExpressionHTC.XR_EYE_EXPRESSION_LEFT_UP_HTC].ToString("F3") + "/" +
            eyes[(int)XrEyeExpressionHTC.XR_EYE_EXPRESSION_LEFT_DOWN_HTC].ToString("F3"));
    }
}
