using System;
using Telemetry.DataReaders;
using Telemetry.Models;
using Telemetry.Session;
using UnityEngine;

namespace Telemetry.Sampling
{
    public class FaceTrackingSampler : MonoBehaviour
    {
        [Header("Sampling Settings")]

        [Tooltip("Face tracking sampling frequency." +
            "VIVE Full Face Tracker facial tracking operates at up to 60 Hz.")]
        [SerializeField]
        [Min(1f)]
        private float sampleRateHz = 60f;

        [Header("References")]

        [SerializeField]
        private FaceReader faceReader;

        [SerializeField]
        private TelemetrySessionManager sessionManager;

        [Header("Debug")]

        [SerializeField]
        private bool samplingActive;

        [SerializeField]
        private double lastSampleTimestamp;

        [SerializeField]
        private int generatedSamples;

        [SerializeField]
        private int skippedNoFreshData;


        private float sampleInterval;
        private float nextSampleTime;

        private bool wasRecording;

        public event Action<FaceTrackingSample>
            OnSampleGenerated;

        public float SampleRateHz => sampleRateHz;

        private void Awake()
        {
            RecalculateInterval();
            ResolveReferences();
        }
        private void OnValidate()
        {
            RecalculateInterval();
        }

        private void RecalculateInterval()
        {
            float rate = (sampleRateHz > 0f ? sampleRateHz : 60f);

            sampleInterval = 1f/rate;
        }

        private void OnEnable()
        {
            TelemetrySourceRegistry.Instance?.RegisterFaceTrackingSampler(this);
        }

        private void OnDisable()
        {
            TelemetrySourceRegistry.Instance?.UnregisterFaceTrackingSampler(this);

            ResetSamplingState();
        }

        private void ResetSamplingState()
        {
            samplingActive = false;

            nextSampleTime = 0f;
            lastSampleTimestamp = 0.0;

            generatedSamples = 0;
            skippedNoFreshData = 0;
        }

        private void ResolveReferences()
        {
            if (faceReader == null)
            {
                faceReader = FaceReader.Instance;
            }

            if (sessionManager == null)
            {
                sessionManager =
                    TelemetrySessionManager.Instance;
            }
        }

        private void AdvanceSchedule(double currentTime)
        {
            nextSampleTime += sampleInterval;

            if (nextSampleTime > currentTime)
                return;

            double behind =
                currentTime - nextSampleTime;

            long missedIntervals =
                (long)Math.Floor(
                    behind / sampleInterval) + 1L;

            nextSampleTime +=
                missedIntervals * sampleInterval;
        }

        private void Update()
        {
            if (sessionManager == null || faceReader == null)
                ResolveReferences();

            if (sessionManager == null)
                return;

            if (!sessionManager.IsRecording)
            {
                if(wasRecording)
                {
                    ResetSamplingState();
                }

                wasRecording = false;
                samplingActive = false;
                return;
            }

            if (faceReader == null)
                return;

            if (!faceReader.IsAvailable)
                return;
            if (sessionManager.Clock == null)
                return;

            double currentTime =
                sessionManager.Clock.CurrentTimeSeconds;

            if (!wasRecording)
            {
                wasRecording = true;
                samplingActive = true;

                RecalculateInterval();

                nextSampleTime = (float)currentTime;
            }

            if (!faceReader.IsAvailable)
                return;

            if (currentTime < nextSampleTime)
                return;

            if (!faceReader.HasFreshDataThisFrame)
            {
                skippedNoFreshData++;
                return;
            }

            GenerateSample(currentTime);
            AdvanceSchedule(currentTime);
        }

        private void GenerateSample(double timestamp)
        {
            samplingActive = true;

            float[] eyesCopy =
                CopyArray(FaceReader.Eyes);

            float[] lipsCopy =
                CopyArray(FaceReader.Lips);

            var sample =
                new FaceTrackingSample(
                    timestamp,
                    eyesCopy,
                    lipsCopy);

            lastSampleTimestamp = timestamp;
            generatedSamples++;

            OnSampleGenerated?.Invoke(sample);
        }

        private float[] CopyArray(float[] source)
        {
            if (source == null)
                return Array.Empty<float>();

            float[] copy = new float[source.Length];

            Array.Copy(source, copy, source.Length);

            return copy;
        }
    }
}