using UnityEngine;
using UnityEngine.XR.OpenXR;
using VIVE.OpenXR.FacialTracking;

    public class FaceEyeReader_archive : MonoBehaviour
    {
        public static FaceEyeReader_archive Instance { get; private set; }

        public static float[] Eyes { get; private set; }
        public static float[] Lips { get; private set; }

        private ViveFacialTracking feature;

        void Awake()
        {
            feature = OpenXRSettings.Instance.GetFeature<ViveFacialTracking>();
            Eyes = new float[(int)ViveFacialTrackingHelper.XR_FACIAL_EXPRESSION_EYE_COUNT_HTC];
            Lips = new float[(int)ViveFacialTrackingHelper.XR_FACIAL_EXPRESSION_LIP_COUNT_HTC];
        }

        void Update()
        {
            if (feature == null || !feature.enabled)
                return;

            // Oczy
            if (feature.GetFacialExpressions(
                    XrFacialTrackingTypeHTC.XR_FACIAL_TRACKING_TYPE_EYE_DEFAULT_HTC,
                    out float[] eyeData))
            {
                //Eyes = eyeData; // referencja do bufora z pluginu moze powodowac problemy przy tworzeniu
                //Eyes nie jest sta³¹ tablic¹ – ka¿da klatka mo¿e wskazywaæ na inn¹ tablicê (nowy obiekt).
                //Ka¿dy inny skrypt, który zrobi³ np.var localRef = FaceEyeReader.Eyes; w Start():
                //nie dostaje aktualnych danych, tylko star¹ tablicê, bo Ty zmieniasz referencjê.


                if (eyeData != null && eyeData.Length == Eyes.Length)
                {
                    System.Array.Copy(eyeData, Eyes, Eyes.Length);
                }
            }

            // Usta
            if (feature.GetFacialExpressions(
                    XrFacialTrackingTypeHTC.XR_FACIAL_TRACKING_TYPE_LIP_DEFAULT_HTC,
                    out float[] lipData))
            {
                //Lips = lipData;
                if (lipData != null && lipData.Length == Lips.Length)
                {
                    System.Array.Copy(lipData, Lips, Lips.Length);
                }
            }
        }
    }