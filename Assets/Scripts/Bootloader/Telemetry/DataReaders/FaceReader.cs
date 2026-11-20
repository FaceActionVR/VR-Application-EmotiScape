using System;
using UnityEngine;
using UnityEngine.XR.OpenXR;
using VIVE.OpenXR.FacialTracking;

namespace Telemetry.DataReaders
{
    public class FaceReader : MonoBehaviour
    {
        public static FaceReader Instance { get; private set; }

        public static float[] Eyes { get; private set; }
        public static float[] Lips { get; private set; }

        [Header("Debug")]
        [SerializeField] private bool featureAvailable;
        [SerializeField] private bool featureEnabled;
        [SerializeField] private bool lastEyeReadSuccess;
        [SerializeField] private bool lastLipReadSuccess;

        [SerializeField] private int lastSuccessfulReadFrame = -1;

        private ViveFacialTracking feature;

        public bool IsAvailable =>
            feature != null && feature.enabled;

        public bool HasEyeData =>
            Eyes != null && Eyes.Length > 0 && lastEyeReadSuccess;

        public bool HasLipData =>
            Lips != null && Lips.Length > 0 && lastLipReadSuccess;

        public bool HasFreshDataThisFrame =>
            lastEyeReadSuccess &&
            lastLipReadSuccess &&
            lastSuccessfulReadFrame == Time.frameCount;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[FaceReader] Duplicate instance destroyed.");
                Destroy(gameObject);
                return;
            }

            Instance = this;

            InitializeBuffers();
            ResolveFeature();
        }

        private void ResolveFeature()
        {
            feature = OpenXRSettings.Instance != null
                 ? OpenXRSettings.Instance.GetFeature<ViveFacialTracking>()
                 : null;

            featureAvailable = feature != null;
            featureEnabled = featureAvailable && feature.enabled;

            if (!featureAvailable)
            {
                Debug.LogWarning("[FaceReader] ViveFacialTracking feature not available.");
            }
        }

        private void InitializeBuffers()
        {
            Eyes = new float[(int)ViveFacialTrackingHelper.XR_FACIAL_EXPRESSION_EYE_COUNT_HTC];
            Lips = new float[(int)ViveFacialTrackingHelper.XR_FACIAL_EXPRESSION_LIP_COUNT_HTC];
        }

        void Update()
        {
            lastEyeReadSuccess = false;
            lastLipReadSuccess = false;

            if (feature == null)
            {
                ResolveFeature();
                if (feature == null)
                    return;
            }

            featureAvailable = true;
            featureEnabled = feature.enabled;

            if (!featureEnabled)
            {
                return;
            }

            ReadEyes();
            ReadLips();

            if (lastEyeReadSuccess && lastLipReadSuccess)
            {
                lastSuccessfulReadFrame = Time.frameCount;
            }
        }

        private void ReadLips()
        {
            if (!feature.GetFacialExpressions(
                   XrFacialTrackingTypeHTC.XR_FACIAL_TRACKING_TYPE_LIP_DEFAULT_HTC,
                   out float[] lipData))
            {
                return;
            }

            if (lipData == null ||
                Lips == null ||
                lipData.Length != Lips.Length)
            {
                return;
            }

            System.Array.Copy(lipData, Lips, Lips.Length);

            lastLipReadSuccess = true;
        }

        private void ReadEyes()
        {
            lastEyeReadSuccess = false;

            if (!feature.GetFacialExpressions(
                     XrFacialTrackingTypeHTC.XR_FACIAL_TRACKING_TYPE_EYE_DEFAULT_HTC,
                     out float[] eyeData))
            {
                return;
            }

            if (eyeData == null ||
                Eyes == null ||
                eyeData.Length != Eyes.Length)
            {
                return;
            }

            System.Array.Copy(eyeData, Eyes, Eyes.Length);
            
            lastEyeReadSuccess= true;
        }
    }
}