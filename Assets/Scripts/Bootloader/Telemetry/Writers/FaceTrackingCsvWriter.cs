using System;
using System.Globalization;
using System.Text;
using Telemetry.Models;
using Telemetry.Sampling;
using Telemetry.Session;
using UnityEngine;
using VIVE.OpenXR.FacialTracking;


namespace Telemetry.Writers
{
    public class FaceTrackingCsvWriter : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TelemetrySessionManager sessionManager;
        [SerializeField] private SessionPathProvider pathProvider;
        [SerializeField] private TelemetrySourceRegistry sourceRegistry;

        [Header("File Settings")]
        [SerializeField] private string fileName = "face_tracking.csv";
        [SerializeField] private BufferedWriterSettings writerSettings = new();

        [Header("Debug")]
        [SerializeField] private bool isOpen;
        [SerializeField] private string currentFilePath;
        [SerializeField] private int writtenSamples;
        [SerializeField] private FaceTrackingSampler activeSampler;

        private BufferedCsvWriter writer;
        private readonly StringBuilder lineBuilder = new();
        public string FileName => fileName;

        private void Awake()
        {
            ResolveReferences();
            writer = new BufferedCsvWriter(writerSettings);
        }

        private void OnEnable()
        {
            ResolveReferences();
            SubscribeSession();
            SubscribeRegistry();

            if (sourceRegistry != null)
            {
                SetActiveSampler(sourceRegistry.CurrentFaceTrackingSampler);
            }
        }

        private void OnDisable()
        {
            UnsubscribeRegistry();
            UnsubscribeSession();
            CloseWriter();
        }

        private void ResolveReferences()
        {
            if (sessionManager == null)
                sessionManager = TelemetrySessionManager.Instance;

            if (pathProvider == null && sessionManager != null)
                pathProvider = sessionManager.GetComponent<SessionPathProvider>();
            if (sourceRegistry == null)
                sourceRegistry = TelemetrySourceRegistry.Instance;
        }

        private void SubscribeSession()
        {
            if (sessionManager != null)
            {
                sessionManager.OnSessionStarted += HandleSessionStarted;
                sessionManager.OnSessionStopping += HandleSessionStopping;
            }
        }

        private void UnsubscribeSession()
        {
            if (sessionManager != null)
            {
                sessionManager.OnSessionStarted -= HandleSessionStarted;
                sessionManager.OnSessionStopping -= HandleSessionStopping;
            }
        }

        private void SubscribeRegistry()
        {
            if (sourceRegistry == null)
                return;

            sourceRegistry.OnFaceTrackingSamplerChanged += HandleSamplerChanged;
        }

        private void UnsubscribeRegistry()
        {
            if (sourceRegistry == null)
                return;

            sourceRegistry.OnFaceTrackingSamplerChanged -= HandleSamplerChanged;
        }

        private void HandleSamplerChanged(FaceTrackingSampler newSampler)
        {
            SetActiveSampler(newSampler);
        }

        private void SetActiveSampler(FaceTrackingSampler newSampler)
        {
            if (activeSampler == newSampler)
                return;

            if (activeSampler != null)
                activeSampler.OnSampleGenerated -= HandleSampleGenerated;

            activeSampler = newSampler;

            if (activeSampler != null)
            {
                activeSampler.OnSampleGenerated += HandleSampleGenerated;
                Debug.Log($"[FaceTrackingCsvWriter] Connected to sampler: {activeSampler.name}");
            }
            else
            {
                Debug.Log("[FaceTrackingCsvWriter] No active sampler.");
            }
        }

        private void HandleSessionStarted(string sessionId, string userId)
        {
            if (pathProvider == null)
            {
                Debug.LogError("[FaceTrackingCsvWriter] Missing SessionPathProvider.");
                return;
            }

            pathProvider.CreateSessionFolder(sessionId);
            currentFilePath = pathProvider.GetFilePath(fileName);

            if (string.IsNullOrWhiteSpace(currentFilePath))
                return;

            writtenSamples = 0;

            writer.Open(currentFilePath, BuildHeader());
            isOpen = writer.IsOpen;

            Debug.Log($"[FaceTrackingCsvWriter] Writing to: {currentFilePath}");
        }

        private void HandleSessionStopping()
        {
            CloseWriter();
        }

        private void HandleSampleGenerated(FaceTrackingSample sample)
        {
            if (writer == null || !writer.IsOpen)
                return;

            writer.WriteLine(BuildCsvLine(sample));
            writtenSamples++;
        }

        private string BuildHeader()
        {
            lineBuilder.Clear();
            lineBuilder.Append("timestamp");

            int eyeCount = GetEyeExpressionCount();
            int lipCount = GetLipExpressionCount();

            for (int i = 0; i < eyeCount; i++)
                lineBuilder.Append(";").Append(GetEyeColumnName(i));

            for (int i = 0; i < lipCount; i++)
                lineBuilder.Append(";").Append(GetLipColumnName(i));

            return lineBuilder.ToString();
        }

        private string BuildCsvLine(FaceTrackingSample sample)
        {
            lineBuilder.Clear();
            lineBuilder.Append(sample.Timestamp.ToString("F4", CultureInfo.InvariantCulture));

            AppendValues(sample.Eyes, GetEyeExpressionCount());
            AppendValues(sample.Lips, GetLipExpressionCount());

            return lineBuilder.ToString();
        }

        private void AppendValues(float[] values, int expectedCount)
        {
            if (values == null)
                return;

            for (int i = 0; i < expectedCount; i++)
            {
                float value = 0f;

                if (i < values.Length)
                    value = values[i];

                lineBuilder.Append(';');
                lineBuilder.Append(value.ToString("F6", CultureInfo.InvariantCulture));
            }
        }

        private int GetEyeExpressionCount()
        {
            return Enum.GetValues(typeof(XrEyeExpressionHTC)).Length;
        }

        private int GetLipExpressionCount()
        {
            return Enum.GetValues(typeof(XrLipExpressionHTC)).Length;
        }

        private void CloseWriter()
        {
            if (writer == null)
                return;

            writer.Close();
            isOpen = false;
        }

        private string GetEyeColumnName(int index)
        {
            if (Enum.IsDefined(typeof(XrEyeExpressionHTC), index))
                return SanitizeColumnName(((XrEyeExpressionHTC)index).ToString());

            return $"eye_{index}";
        }

        private string GetLipColumnName(int index)
        {
            if (Enum.IsDefined(typeof(XrLipExpressionHTC), index))
                return SanitizeColumnName(((XrLipExpressionHTC)index).ToString());

            return $"lip_{index}";
        }

        private string SanitizeColumnName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "unknown";

            return value
                .Replace("XR_EYE_EXPRESSION_", "")
                .Replace("XR_LIP_EXPRESSION_", "")
                .Replace("_HTC", "")
                .ToLowerInvariant();
        }
    }
}