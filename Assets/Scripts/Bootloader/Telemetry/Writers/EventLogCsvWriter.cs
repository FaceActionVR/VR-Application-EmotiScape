using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Telemetry.Session;
using UnityEngine;

namespace Telemetry.Writers
{
    public class EventLogCsvWriter : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TelemetrySessionManager sessionManager;
        [SerializeField] private SessionPathProvider pathProvider;

        [Header("File Settings")]
        [SerializeField] private string fileName = "events_log.csv";
        [SerializeField] private BufferedWriterSettings writerSettings = new();

        [Header("Debug")]
        [SerializeField] private bool isOpen;
        [SerializeField] private string currentFilePath;
        [SerializeField] private int writtenEvents;

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
        }

        private void OnDisable()
        {
            UnsubscribeSession();
            CloseWriter();
        }

        private void ResolveReferences()
        {
            if (sessionManager == null)
                sessionManager = TelemetrySessionManager.Instance;

            if (pathProvider == null && sessionManager != null)
                pathProvider = sessionManager.GetComponent<SessionPathProvider>();
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

        private void HandleSessionStarted(string sessionId, string userId)
        {
            if (pathProvider == null)
            {
                Debug.LogError("[EventsLogCsvWriter] Missing SessionPathProvider.");
                return;
            }

            pathProvider.CreateSessionFolder(sessionId);
            currentFilePath = pathProvider.GetFilePath(fileName);

            if (string.IsNullOrWhiteSpace(currentFilePath))
                return;

            writtenEvents = 0;

            writer.Open(currentFilePath, BuildHeader());
            isOpen = writer.IsOpen;

            Debug.Log($"[EventsLogCsvWriter] Writing to: {currentFilePath}");
        }

        private void HandleSessionStopping()
        {
            CloseWriter();
        }

        public void LogEvent(
           double timestampStart,
           double timestampEnd,
           string eventType,
           string targetObject = "",
           double duration = 0,
           int roomId = -1,
           string extraJson = "{}")
        {
            if (writer == null || !writer.IsOpen)
                return;

            writer.WriteLine(BuildCsvLine(
                timestampStart,
                timestampEnd,
                eventType,
                targetObject,
                duration,
                roomId,
                extraJson
            ));

            writtenEvents++;
        }

        public void LogInstantEvent(
            double timestamp,
            string eventType,
            string targetObject = "",
            int roomId = -1,
            string extraJson = "{}")
        {
            LogEvent(
                timestamp,
                timestamp,
                eventType,
                targetObject,
                0,
                roomId,
                extraJson
            );
        }

        private string BuildHeader()
        {
            return "timestamp_start;timestamp_end;event_type;target_object;duration;room_id;extra_json";
        }

        private string BuildCsvLine(
            double timestampStart,
            double timestampEnd,
            string eventType,
            string targetObject,
            double duration,
            int roomId,
            string extraJson)
        {
            lineBuilder.Clear();

            lineBuilder.Append(timestampStart.ToString("F4", CultureInfo.InvariantCulture));
            lineBuilder.Append(';').Append(timestampEnd.ToString("F4", CultureInfo.InvariantCulture));
            lineBuilder.Append(';').Append(EscapeCsv(eventType));
            lineBuilder.Append(';').Append(EscapeCsv(targetObject));
            lineBuilder.Append(';').Append(duration.ToString("F4", CultureInfo.InvariantCulture));
            lineBuilder.Append(';').Append(roomId);
            lineBuilder.Append(';').Append(EscapeCsv(extraJson));

            return lineBuilder.ToString();
        }

        private string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            bool mustQuote =
                value.Contains(";") ||
                value.Contains("\"") ||
                value.Contains("\n") ||
                value.Contains("\r");

            if (!mustQuote)
                return value;

            return "\"" + value.Replace("\"", "\"\"") + "\"";
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