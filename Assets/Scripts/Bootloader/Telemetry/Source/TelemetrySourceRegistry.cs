using UnityEngine;
using System;
using Telemetry.Sampling;

namespace Telemetry.Session
{
    public class TelemetrySourceRegistry : MonoBehaviour
    {
        public static TelemetrySourceRegistry Instance { get; private set; }

        [Header("Current Scene Sources")]

        [SerializeField]
        private FaceTrackingSampler currentFaceTrackingSampler;

        [SerializeField]
        private PhysicsTrackingSampler currentPhysicsTrackingSampler;

        public FaceTrackingSampler CurrentFaceTrackingSampler =>
            currentFaceTrackingSampler;

        public PhysicsTrackingSampler CurrentPhysicsTrackingSampler =>
            currentPhysicsTrackingSampler;

        public event Action<FaceTrackingSampler> OnFaceTrackingSamplerChanged;
        public event Action<PhysicsTrackingSampler> OnPhysicsTrackingSamplerChanged;

        public float LastKnownFaceSampleRateHz { get; private set; } = -1f;
        public float LastKnownPhysicsSampleRateHz { get; private set; } = -1f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[TelemetrySourceRegistry] Duplicate instance destroyed.");
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        public void RegisterFaceTrackingSampler(FaceTrackingSampler sampler)
        {
            if (sampler == null)
            {
                Debug.LogWarning("[TelemetrySourceRegistry] Tried to register null FaceTrackingSampler.");
                return;
            }

            if (currentFaceTrackingSampler == sampler)
                return;

            currentFaceTrackingSampler = sampler;

            Debug.Log($"[TelemetrySourceRegistry] FaceTrackingSampler registered: {sampler.name}");
            LastKnownFaceSampleRateHz = sampler.SampleRateHz;
            OnFaceTrackingSamplerChanged?.Invoke(currentFaceTrackingSampler);
        }

        public void UnregisterFaceTrackingSampler(FaceTrackingSampler sampler)
        {
            if (sampler == null)
                return;

            if (currentFaceTrackingSampler != sampler)
                return;

            Debug.Log($"[TelemetrySourceRegistry] FaceTrackingSampler unregistered: {sampler.name}");

            currentFaceTrackingSampler = null;

            OnFaceTrackingSamplerChanged?.Invoke(null);
        }

        public void RegisterPhysicsTrackingSampler(PhysicsTrackingSampler sampler)
        {
            if (sampler == null)
            {
                Debug.LogWarning("[TelemetrySourceRegistry] Tried to register null PhysicsTrackingSampler.");
                return;
            }

            if (currentPhysicsTrackingSampler == sampler)
                return;

            currentPhysicsTrackingSampler = sampler;

            Debug.Log($"[TelemetrySourceRegistry] PhysicsTrackingSampler registered: {sampler.name}");
            LastKnownPhysicsSampleRateHz = sampler.SampleRateHz;
            OnPhysicsTrackingSamplerChanged?.Invoke(currentPhysicsTrackingSampler);
        }

        public void UnregisterPhysicsTrackingSampler(PhysicsTrackingSampler sampler)
        {
            if (sampler == null)
                return;

            if (currentPhysicsTrackingSampler != sampler)
                return;

            Debug.Log($"[TelemetrySourceRegistry] PhysicsTrackingSampler unregistered: {sampler.name}");

            currentPhysicsTrackingSampler = null;

            OnPhysicsTrackingSamplerChanged?.Invoke(null);
        }

        public void ClearAll()
        {
            currentFaceTrackingSampler = null;
            currentPhysicsTrackingSampler = null;

            OnFaceTrackingSamplerChanged?.Invoke(null);
            OnPhysicsTrackingSamplerChanged?.Invoke(null);

            Debug.Log("[TelemetrySourceRegistry] Cleared all scene sources.");
        }
    }
}