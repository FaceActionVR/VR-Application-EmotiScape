using System;
using UnityEngine;

namespace Telemetry.Session
{
    public class TelemetrySessionManager : MonoBehaviour
    {
        public static TelemetrySessionManager Instance
        {
            get;
            private set;
        }

        [Header("Session State")]

        [SerializeField]
        private bool isRecording = false;

        [SerializeField]
        private string currentSessionId;

        [SerializeField]
        private string currentUserId;

        [Header("References")]

        [SerializeField]
        private SessionClock sessionClock;

        public bool IsRecording => isRecording;

        public string CurrentSessionId =>
            currentSessionId;

        public string CurrentUserId =>
            currentUserId;

        public SessionClock Clock =>
            sessionClock;

        /// <summary>
        /// Fired after session starts.
        /// Args:
        /// sessionId, userId
        /// </summary>
        public event Action<string, string>
            OnSessionStarted;

        /// <summary>
        /// Fired before session fully stops.
        /// Useful for flush/save operations.
        /// </summary>
        public event Action
            OnSessionStopping;

        /// <summary>
        /// Fired after session fully stopped.
        /// </summary>
        public event Action
            OnSessionStopped;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning(
                    "[TelemetrySessionManager] " +
                    "Duplicate instance destroyed.");

                Destroy(gameObject);
                return;
            }

            Instance = this;

            ValidateReferences();
        }

        private void ValidateReferences()
        {
            if (sessionClock == null)
            {
                sessionClock =
                    GetComponent<SessionClock>();
            }

            if (sessionClock == null)
            {
                Debug.LogError(
                    "[TelemetrySessionManager] " +
                    "Missing SessionClock reference.");
            }
        }

        /// <summary>
        /// Starts telemetry recording session.
        /// </summary>
        public void StartSession(
            string sessionId,
            string userId = "unknown")
        {
            if (isRecording)
            {
                Debug.LogWarning(
                    "[TelemetrySessionManager] " +
                    "Session already running.");

                return;
            }

            currentSessionId = sessionId;
            currentUserId = userId;

            isRecording = true;

            sessionClock?.StartClock();

            Debug.Log(
                $"[TelemetrySessionManager] " +
                $"Session started: {sessionId}");

            OnSessionStarted?.Invoke(
                sessionId,
                userId);
        }

        /// <summary>
        /// Stops telemetry recording session.
        /// </summary>
        public void StopSession()
        {
            if (!isRecording)
            {
                Debug.LogWarning(
                    "[TelemetrySessionManager] " +
                    "No active session.");

                return;
            }

            Debug.Log(
                $"[TelemetrySessionManager] " +
                $"Stopping session: {currentSessionId}");

            OnSessionStopping?.Invoke();

            sessionClock?.StopClock();

            isRecording = false;

            OnSessionStopped?.Invoke();

            Debug.Log(
                $"[TelemetrySessionManager] " +
                $"Session stopped: {currentSessionId}");
        }
    }
}