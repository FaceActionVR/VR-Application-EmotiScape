using UnityEngine;

namespace Telemetry.Session
{
    public class SessionClock : MonoBehaviour
    {
        public static SessionClock Instance { get; private set; }

        [Header("Clock Settings")]

        [Tooltip("Automatically reset clock when starting.")]
        [SerializeField]
        private bool resetOnStart = true;

        [Header("Debug")]

        [SerializeField]
        private bool clockRunning = false;

        [SerializeField]
        private double sessionStartRealtime;

        [SerializeField]
        private double currentSessionTimeSeconds;

        public bool IsRunning => clockRunning;

        /// <summary>
        /// Current session-relative timestamp in seconds.
        /// </summary>
        public double CurrentTimeSeconds =>
            currentSessionTimeSeconds;

        /// <summary>
        /// Absolute Unity realtime timestamp.
        /// </summary>
        public double AbsoluteRealtimeSeconds =>
            Time.realtimeSinceStartupAsDouble;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning(
                    "[SessionClock] Duplicate instance destroyed.");

                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void Update()
        {
            if (!clockRunning)
                return;

            currentSessionTimeSeconds =
                Time.realtimeSinceStartupAsDouble -
                sessionStartRealtime;
        }

        /// <summary>
        /// Starts session-relative clock.
        /// </summary>
        public void StartClock()
        {
            if (resetOnStart)
            {
                ResetClock();
            }

            sessionStartRealtime =
                Time.realtimeSinceStartupAsDouble;

            clockRunning = true;

            Debug.Log(
                "[SessionClock] Clock started.");
        }

        /// <summary>
        /// Stops clock updates.
        /// </summary>
        public void StopClock()
        {
            if (!clockRunning)
                return;

            clockRunning = false;

            Debug.Log(
                $"[SessionClock] Clock stopped at " +
                $"{currentSessionTimeSeconds:F3}s");
        }

        /// <summary>
        /// Resets session-relative time.
        /// </summary>
        public void ResetClock()
        {
            currentSessionTimeSeconds = 0;
            sessionStartRealtime = 0;

            Debug.Log(
                "[SessionClock] Clock reset.");
        }
    }
}