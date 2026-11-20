using System;
using Telemetry.Models;
using Telemetry.Session;
using Telemetry.DataReaders;
using Telemetry.SceneState;
using UnityEngine;

namespace Telemetry.Sampling
{
    public class PhysicsTrackingSampler : MonoBehaviour
    {
        [Header("Sampling Settings")]
        [SerializeField] private float sampleRateHz = 10f;

        [Header("References")]
        [SerializeField] private TelemetrySessionManager sessionManager;
        [SerializeField] private HeadTrackingReader headReader;

        [Header("Room")]
        [SerializeField] private RoomStateProvider roomStateProvider;

        [Header("Debug")]
        [SerializeField] private bool samplingActive;
        [SerializeField] private double lastSampleTimestamp;
        [SerializeField] private bool logGeneratedSamples = false;

        private float sampleInterval;
        private double nextSampleTime;

        public event Action<PhysicsTrackingSample> OnSampleGenerated;

        private void Awake()
        {
            sampleInterval = sampleRateHz > 0f ? 1f / sampleRateHz : 0.1f;
            ResolveReferences();
        }

        public float SampleRateHz => sampleRateHz;

        private void ResolveReferences()
        {
            if (roomStateProvider == null)
                roomStateProvider = FindObjectOfType<RoomStateProvider>();

            if (sessionManager == null)
                sessionManager = TelemetrySessionManager.Instance;

            if (headReader == null)
                headReader = FindObjectOfType<HeadTrackingReader>();
        }

        private void OnEnable()
        {
            TelemetrySourceRegistry.Instance?.RegisterPhysicsTrackingSampler(this);
        }

        private void OnDisable()
        {
            TelemetrySourceRegistry.Instance?.UnregisterPhysicsTrackingSampler(this);
        }

        private void Update()
        {
            if (sessionManager == null || headReader == null)
            {
                ResolveReferences();
                return;
            }

            if (!sessionManager.IsRecording)
            {
                samplingActive = false;
                return;
            }

            if (sessionManager.Clock == null)
                return;

            if (headReader == null || !headReader.HasValidTransform)
                return;

            double currentTime = sessionManager.Clock.CurrentTimeSeconds;

            if (currentTime < nextSampleTime)
                return;

            nextSampleTime = currentTime + sampleInterval;

            GenerateSample(currentTime);
        }

        private void GenerateSample(double timestamp)
        {
            samplingActive = true;
            lastSampleTimestamp = timestamp;

            int roomId = roomStateProvider != null ? roomStateProvider.CurrentRoomId : -1;

            var sample = new PhysicsTrackingSample(
                timestamp,
                roomId,
                headReader.CurrentPosition,
                headReader.CurrentRotation
            );

            OnSampleGenerated?.Invoke(sample);
        }
    }
}