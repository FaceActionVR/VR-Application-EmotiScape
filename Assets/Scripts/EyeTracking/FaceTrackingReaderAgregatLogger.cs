using UnityEngine;
using UnityEngine.XR.OpenXR;
using VIVE.OpenXR.FacialTracking;

public class FaceTrackingReaderAgregatLogger : MonoBehaviour
{
    [Header("Sampling")]
    [Tooltip("Docelowa liczba próbek na sekundê (np. 20 fps to 20).")]
    [SerializeField] private float samplesPerSecond = 20f;

    [Tooltip("Minimalna zmiana (0-1) wymagana do zalogowania nowej próbki twarzy.")]
    [SerializeField] private float lipChangeThreshold = 0.01f;
    [Tooltip("Minimalna zmiana (0-1) wymagana do zalogowania nowej próbki oczu.")]
    [SerializeField] private float eyeChangeThreshold = 0.05f;

    // G³ówny feature VIVE OpenXR odpowiedzialny za odczyt ekspresji
    private ViveFacialTracking faceFeature;

    // Bufory na dane ekspresji ust i oczu (rozmiary wynikaj¹ z definicji enumów SDK)
    private float[] lips = new float[(int)XrLipExpressionHTC.XR_LIP_EXPRESSION_MAX_ENUM_HTC];
    private float[] eyes = new float[(int)XrEyeExpressionHTC.XR_EYE_EXPRESSION_MAX_ENUM_HTC];

    // Bufory na ostatnio zalogowane wartoœci do wykrywania istotnych zmian
    private float[] lastLoggedLips;
    private float[] lastLoggedEyes;

    // Agregatory próbek do wyliczania œrednich/min/max
    private ExpressionAggregator lipAggregator;
    private ExpressionAggregator eyeAggregator;

    // Jednorazowe flagi do logowania b³êdów kompatybilnoœci/konfiguracji
    private bool loggedMissingFeature;
    private bool loggedLipError;
    private bool loggedEyeError;

    private float sampleInterval;
    private float lastSampleTime;

    private void Start()
    {
        // Uwaga: ten projekt u¿ywa w³asnego œrodowiska (stanowisko standalone na Vive Elite XR)
        // i mo¿e ró¿niæ siê od przyk³adowych skryptów z katalogu Wzroce.
        faceFeature = OpenXRSettings.Instance.GetFeature<ViveFacialTracking>();

        lipAggregator = new ExpressionAggregator(lips.Length);
        eyeAggregator = new ExpressionAggregator(eyes.Length);

        sampleInterval = samplesPerSecond > 0 ? 1f / samplesPerSecond : 0.05f; // domyœlnie ~20 fps
        lastSampleTime = Time.time;
        Debug.Log("[FaceTrackingReader] Start");
        if (faceFeature == null || !faceFeature.enabled)
        {
            loggedMissingFeature = true;
            Debug.LogError("[FaceTrackingReader] Nie wykryto feature ViveFacialTracking. SprawdŸ: VIVE OpenXR 2.5.1, w³¹czone facial tracking w ustawieniach OpenXR i tryb standalone na goglach.");
        }
    }

    private void Update()
    {
        if (Time.time - lastSampleTime < sampleInterval)
        {
            return; // ograniczenie logów do ~samplesPerSecond
        }

        if (faceFeature == null || !faceFeature.enabled)
        {
            return;
        }

        lastSampleTime = Time.time;

        // Próba pobrania ekspresji ust
        if (faceFeature.GetFacialExpressions(XrFacialTrackingTypeHTC.XR_FACIAL_TRACKING_TYPE_LIP_DEFAULT_HTC, out float[] lipData))
        {
            lips = lipData;
            lipAggregator.AddSample(lips);
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
            eyeAggregator.AddSample(eyes);
        }
        else if (!loggedEyeError)
        {
            loggedEyeError = true;
            Debug.LogError("[FaceTrackingReader] Eye expressions nie s¹ dostêpne. Rozwi¹zanie: sprawdŸ, czy w³¹czono Eye Tracking w ustawieniach OpenXR i czy urz¹dzenie je wspiera.");
        }

        LogAggregatedSnapshots();
    }

    // Logowanie agregowanych wartoœci ust i oczu z filtracj¹ drobnych zmian
    private void LogAggregatedSnapshots()
    {
        if (lipAggregator.HasData)
        {
            float[] avgLip = lipAggregator.GetAverage();
            if (ShouldLog(avgLip, ref lastLoggedLips, new[]
                {
                    (int)XrLipExpressionHTC.XR_LIP_EXPRESSION_JAW_OPEN_HTC,
                    (int)XrLipExpressionHTC.XR_LIP_EXPRESSION_MOUTH_UPPER_UPRIGHT_HTC,
                    (int)XrLipExpressionHTC.XR_LIP_EXPRESSION_MOUTH_UPPER_UPLEFT_HTC
                }, lipChangeThreshold))
            {
                Debug.Log(
                    "[FaceTrackingReader] Usta (avg/min/max) - JawOpen: " +
                    FormatTriple(avgLip, lipAggregator.GetMin(), lipAggregator.GetMax(), (int)XrLipExpressionHTC.XR_LIP_EXPRESSION_JAW_OPEN_HTC) +
                    ", Smile R/L: " +
                    FormatTriple(avgLip, lipAggregator.GetMin(), lipAggregator.GetMax(), (int)XrLipExpressionHTC.XR_LIP_EXPRESSION_MOUTH_UPPER_UPRIGHT_HTC) + "/" +
                    FormatTriple(avgLip, lipAggregator.GetMin(), lipAggregator.GetMax(), (int)XrLipExpressionHTC.XR_LIP_EXPRESSION_MOUTH_UPPER_UPLEFT_HTC));
            }

            lipAggregator.Reset();
        }

        if (eyeAggregator.HasData)
        {
            float[] avgEye = eyeAggregator.GetAverage();
            if (ShouldLog(avgEye, ref lastLoggedEyes, new[]
                {
                    (int)XrEyeExpressionHTC.XR_EYE_EXPRESSION_LEFT_BLINK_HTC,
                    (int)XrEyeExpressionHTC.XR_EYE_EXPRESSION_RIGHT_BLINK_HTC,
                    (int)XrEyeExpressionHTC.XR_EYE_EXPRESSION_LEFT_UP_HTC,
                    (int)XrEyeExpressionHTC.XR_EYE_EXPRESSION_LEFT_DOWN_HTC
                }, eyeChangeThreshold))
            {
                Debug.Log(
                    "[FaceTrackingReader] Oczy (avg/min/max) - Blink L/R: " +
                    FormatTriple(avgEye, eyeAggregator.GetMin(), eyeAggregator.GetMax(), (int)XrEyeExpressionHTC.XR_EYE_EXPRESSION_LEFT_BLINK_HTC) + "/" +
                    FormatTriple(avgEye, eyeAggregator.GetMin(), eyeAggregator.GetMax(), (int)XrEyeExpressionHTC.XR_EYE_EXPRESSION_RIGHT_BLINK_HTC) +
                    ", L Up/Down: " +
                    FormatTriple(avgEye, eyeAggregator.GetMin(), eyeAggregator.GetMax(), (int)XrEyeExpressionHTC.XR_EYE_EXPRESSION_LEFT_UP_HTC) + "/" +
                    FormatTriple(avgEye, eyeAggregator.GetMin(), eyeAggregator.GetMax(), (int)XrEyeExpressionHTC.XR_EYE_EXPRESSION_LEFT_DOWN_HTC));
            }

            eyeAggregator.Reset();
        }
    }

    private bool ShouldLog(float[] averages, ref float[] lastLogged, int[] keyIndices, float changeThreshold)
    {
        if (lastLogged == null || lastLogged.Length != averages.Length)
        {
            lastLogged = (float[])averages.Clone();
            return true; // pierwszy zapis
        }

        bool significant = false;

        foreach (int index in keyIndices)
        {
            float delta = Mathf.Abs(averages[index] - lastLogged[index]);
            if (delta >= changeThreshold)
            {
                significant = true;
                break;
            }
        }

        if (significant)
        {
            for (int i = 0; i < averages.Length; i++)
            {
                lastLogged[i] = averages[i];
            }
        }

        return significant;
    }

    private string FormatTriple(float[] avg, float[] min, float[] max, int index)
    {
        return avg[index].ToString("F3") + " (min " + min[index].ToString("F3") + ", max " + max[index].ToString("F3") + ")";
    }

    // Prosty agregator do liczenia œredniej, min i max na przestrzeni próbek
    private class ExpressionAggregator
    {
        private readonly float[] sum;
        private readonly float[] min;
        private readonly float[] max;
        private int count;

        public bool HasData => count > 0;

        public ExpressionAggregator(int size)
        {
            sum = new float[size];
            min = new float[size];
            max = new float[size];
            Reset();
        }

        public void AddSample(float[] values)
        {
            if (values == null || values.Length != sum.Length)
            {
                return;
            }

            for (int i = 0; i < values.Length; i++)
            {
                sum[i] += values[i];
                if (values[i] < min[i])
                {
                    min[i] = values[i];
                }
                if (values[i] > max[i])
                {
                    max[i] = values[i];
                }
            }

            count++;
        }

        public float[] GetAverage()
        {
            float[] avg = new float[sum.Length];
            if (count == 0)
            {
                return avg;
            }

            for (int i = 0; i < sum.Length; i++)
            {
                avg[i] = sum[i] / count;
            }

            return avg;
        }

        public float[] GetMin()
        {
            float[] result = new float[min.Length];
            for (int i = 0; i < min.Length; i++)
            {
                result[i] = min[i];
            }
            return result;
        }

        public float[] GetMax()
        {
            float[] result = new float[max.Length];
            for (int i = 0; i < max.Length; i++)
            {
                result[i] = max[i];
            }
            return result;
        }

        public void Reset()
        {
            for (int i = 0; i < sum.Length; i++)
            {
                sum[i] = 0f;
                min[i] = float.MaxValue;
                max[i] = float.MinValue;
            }

            count = 0;
        }
    }
}
