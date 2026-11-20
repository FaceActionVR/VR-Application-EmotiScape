using Telemetry.Models;
using Telemetry.Sampling;
using UnityEngine;

namespace Telemetry.Debuger
{
    public class FaceTrackingDebugListener : MonoBehaviour
    {
        [Header("References")]

        [SerializeField]
        private FaceTrackingSampler sampler;

        private void Awake()
        {
            if (sampler == null)
            {
                sampler =
                    FindObjectOfType<FaceTrackingSampler>();
            }
        }

        private void OnEnable()
        {
            if (sampler != null)
            {
                sampler.OnSampleGenerated += HandleSample;
            }
        }

        private void OnDisable()
        {
            if (sampler != null)
            {
                sampler.OnSampleGenerated -= HandleSample;
            }
        }

        private void HandleSample(
            FaceTrackingSample sample)
        {
            Debug.Log(
                $"[FaceTrackingDebug] " +
                $"Sample @ {sample.Timestamp:F3}s");
        }
    }
}