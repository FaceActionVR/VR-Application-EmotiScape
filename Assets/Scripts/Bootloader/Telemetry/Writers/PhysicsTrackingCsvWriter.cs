using System.Globalization;
using System.Text;
using Telemetry.Models;
using Telemetry.DataReaders;
using Telemetry.Sampling;
using Telemetry.Session;
using UnityEngine;

namespace Telemetry.Writers
{
    public class PhysicsTrackingCsvWriter : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TelemetrySessionManager sessionManager;
        [SerializeField] private SessionPathProvider pathProvider;
        [SerializeField] private TelemetrySourceRegistry sourceRegistry;

        [Header("File Settings")]
        [SerializeField] private string fileName = "physics_tracking.csv";
        [SerializeField] private BufferedWriterSettings writerSettings = new();

        [Header("Debug")]
        [SerializeField] private bool isOpen;
        [SerializeField] private string currentFilePath;
        [SerializeField] private int writtenSamples;
        [SerializeField] private PhysicsTrackingSampler activeSampler;

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
                SetActiveSampler(sourceRegistry.CurrentPhysicsTrackingSampler);
        }

        private void OnDisable()
        {
            UnsubscribeSession();
            UnsubscribeRegistry();
            SetActiveSampler(null);

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
            if (sessionManager == null)
                return;

            sessionManager.OnSessionStarted += HandleSessionStarted;
            sessionManager.OnSessionStopping += HandleSessionStopping;
        }

        private void UnsubscribeSession()
        {
            if (sessionManager == null)
                return;

            sessionManager.OnSessionStarted -= HandleSessionStarted;
            sessionManager.OnSessionStopping -= HandleSessionStopping;
        }

        private void SubscribeRegistry()
        {
            if (sourceRegistry == null)
                return;

            sourceRegistry.OnPhysicsTrackingSamplerChanged += HandleSamplerChanged;
        }

        private void UnsubscribeRegistry()
        {
            if (sourceRegistry == null)
                return;

            sourceRegistry.OnPhysicsTrackingSamplerChanged -= HandleSamplerChanged;
        }

        private void HandleSamplerChanged(PhysicsTrackingSampler newSampler)
        {
            SetActiveSampler(newSampler);
        }

        private void SetActiveSampler(PhysicsTrackingSampler newSampler)
        {
            if (activeSampler == newSampler)
                return;

            if (activeSampler != null)
                activeSampler.OnSampleGenerated -= HandleSampleGenerated;

            activeSampler = newSampler;

            if (activeSampler != null)
            {
                activeSampler.OnSampleGenerated += HandleSampleGenerated;
                Debug.Log($"[PhysicsTrackingCsvWriter] Connected to sampler: {activeSampler.name}");
            }
            else
            {
                Debug.Log("[PhysicsTrackingCsvWriter] No active sampler.");
            }
        }

        private void HandleSessionStarted(string sessionId, string userId)
        {
            if (pathProvider == null)
            {
                Debug.LogError("[PhysicsTrackingCsvWriter] Missing SessionPathProvider.");
                return;
            }

            pathProvider.CreateSessionFolder(sessionId);
            currentFilePath = pathProvider.GetFilePath(fileName);

            if (string.IsNullOrWhiteSpace(currentFilePath))
                return;

            writtenSamples = 0;
            writer.Open(currentFilePath, BuildHeader());
            isOpen = writer.IsOpen;

            Debug.Log($"[PhysicsTrackingCsvWriter] Writing to: {currentFilePath}");
        }

        private void HandleSessionStopping()
        {
            CloseWriter();
        }

        private void HandleSampleGenerated(PhysicsTrackingSample sample)
        {
            if (writer == null || !writer.IsOpen)
                return;

            writer.WriteLine(BuildCsvLine(sample));
            writtenSamples++;
        }

        private string BuildHeader()
        {
            return "timestamp;room_id;head_x;head_y;head_z;rot_x;rot_y;rot_z;rot_w";
        }

        private string BuildCsvLine(PhysicsTrackingSample sample)
        {
            lineBuilder.Clear();

            lineBuilder.Append(sample.Timestamp.ToString("F4", CultureInfo.InvariantCulture));
            lineBuilder.Append(';').Append(sample.RoomId);

            lineBuilder.Append(';').Append(sample.HeadPosition.x.ToString("F6", CultureInfo.InvariantCulture));
            lineBuilder.Append(';').Append(sample.HeadPosition.y.ToString("F6", CultureInfo.InvariantCulture));
            lineBuilder.Append(';').Append(sample.HeadPosition.z.ToString("F6", CultureInfo.InvariantCulture));

            lineBuilder.Append(';').Append(sample.HeadRotation.x.ToString("F6", CultureInfo.InvariantCulture));
            lineBuilder.Append(';').Append(sample.HeadRotation.y.ToString("F6", CultureInfo.InvariantCulture));
            lineBuilder.Append(';').Append(sample.HeadRotation.z.ToString("F6", CultureInfo.InvariantCulture));
            lineBuilder.Append(';').Append(sample.HeadRotation.w.ToString("F6", CultureInfo.InvariantCulture));

            return lineBuilder.ToString();
        }

        private void CloseWriter()
        {
            if (writer == null)
                return;

            writer.Close();
            isOpen = false;
        }
    }
}