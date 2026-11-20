using System;
using System.Collections.Generic;
using Telemetry.SceneState;
using Telemetry.Session;
using Telemetry.Writers;
using UnityEngine;

namespace Telemetry.Interactions
{
    public class InteractionEventLogger : MonoBehaviour
    {
        private const string LogPrefix = "[INTERACTION_EVENTS]";
        private const string EventTypeObjectManipulation = "OBJECT_MANIPULATION";

        [Header("Telemetry")]
        [SerializeField] private EventLogCsvWriter eventsWriter;

        [Header("Debug")]
        [SerializeField] private bool debugLogs = true;

        private readonly Dictionary<int, ActiveManipulation> activeManipulations =
            new Dictionary<int, ActiveManipulation>();

        private void Awake()
        {
            ResolveReferences();
        }

        private void ResolveReferences()
        {
            if (eventsWriter == null)
                eventsWriter = FindObjectOfType<EventLogCsvWriter>();
        }

        public void BeginObjectManipulation(
            GameObject sourceObject,
            string targetObject,
            string extraJson = "{}")
        {
            if (sourceObject == null)
                return;

            if (string.IsNullOrWhiteSpace(targetObject))
            {
                Warn("BeginObjectManipulation ignored: targetObject is empty.");
                return;
            }

            if (!TryGetSessionTime(out double timestamp))
                return;

            int key = sourceObject.GetInstanceID();

            if (activeManipulations.ContainsKey(key))
                return;

            int roomId = GetCurrentRoomId();

            ActiveManipulation manipulation = new ActiveManipulation
            {
                TargetObject = targetObject,
                StartTime = timestamp,
                RoomId = roomId,
                ExtraJson = NormalizeExtraJson(extraJson)
            };

            activeManipulations.Add(key, manipulation);

            Log(
                $"BEGIN | Target={targetObject} | " +
                $"Time={timestamp:F3} | Room={roomId}");
        }

        public void EndObjectManipulation(GameObject sourceObject)
        {
            if (sourceObject == null)
                return;

            int key = sourceObject.GetInstanceID();

            if (!activeManipulations.TryGetValue(
                    key,
                    out ActiveManipulation manipulation))
            {
                return;
            }

            activeManipulations.Remove(key);

            if (!TryGetSessionTime(out double endTime))
                return;

            ResolveReferences();

            if (eventsWriter == null)
            {
                Warn(
                    $"EventLogCsvWriter not found. " +
                    $"OBJECT_MANIPULATION not written | Target={manipulation.TargetObject}");
                return;
            }

            double duration = Math.Max(
                0d,
                endTime - manipulation.StartTime);

            eventsWriter.LogEvent(
                manipulation.StartTime,
                endTime,
                EventTypeObjectManipulation,
                manipulation.TargetObject,
                duration,
                manipulation.RoomId,
                manipulation.ExtraJson
            );

            Log(
                $"END | Target={manipulation.TargetObject} | " +
                $"Start={manipulation.StartTime:F3} | " +
                $"End={endTime:F3} | " +
                $"Duration={duration:F3} | " +
                $"Room={manipulation.RoomId}");
        }

        public void LogPointEvent(
            string eventType,
            string targetObject,
            string extraJson = "{}")
        {
            if (string.IsNullOrWhiteSpace(eventType))
            {
                Warn("LogPointEvent ignored: eventType is empty.");
                return;
            }

            if (!TryGetSessionTime(out double timestamp))
                return;

            ResolveReferences();

            if (eventsWriter == null)
            {
                Warn(
                    $"EventLogCsvWriter not found. " +
                    $"Event={eventType} not written.");
                return;
            }

            int roomId = GetCurrentRoomId();

            eventsWriter.LogEvent(
                timestamp,
                timestamp,
                eventType,
                targetObject ?? "",
                0.0,
                roomId,
                NormalizeExtraJson(extraJson)
            );

            Log(
                $"POINT | Event={eventType} | " +
                $"Target={targetObject} | " +
                $"Time={timestamp:F3} | Room={roomId}");
        }

        public void FlushActiveManipulations()
        {
            if (activeManipulations.Count == 0)
                return;

            if (!TryGetSessionTime(out double endTime))
            {
                activeManipulations.Clear();
                return;
            }

            ResolveReferences();

            if (eventsWriter == null)
            {
                activeManipulations.Clear();
                return;
            }

            List<ActiveManipulation> manipulations =
                new List<ActiveManipulation>(activeManipulations.Values);

            activeManipulations.Clear();

            foreach (ActiveManipulation manipulation in manipulations)
            {
                double duration = Math.Max(
                    0d,
                    endTime - manipulation.StartTime);

                eventsWriter.LogEvent(
                    manipulation.StartTime,
                    endTime,
                    EventTypeObjectManipulation,
                    manipulation.TargetObject,
                    duration,
                    manipulation.RoomId,
                    manipulation.ExtraJson
                );

                Log(
                    $"FLUSH | Target={manipulation.TargetObject} | " +
                    $"Duration={duration:F3}");
            }
        }

        private bool TryGetSessionTime(out double timestamp)
        {
            timestamp = 0d;

            TelemetrySessionManager manager =
                TelemetrySessionManager.Instance;

            if (manager == null)
                return false;

            if (manager.Clock == null)
                return false;

            if (!manager.Clock.IsRunning)
                return false;

            timestamp = manager.Clock.CurrentTimeSeconds;
            return true;
        }

        private int GetCurrentRoomId()
        {
            RoomStateProvider roomStateProvider =
                FindObjectOfType<RoomStateProvider>();

            return roomStateProvider != null
                ? roomStateProvider.CurrentRoomId
                : -1;
        }

        private string NormalizeExtraJson(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "{}"
                : value;
        }

        private void Log(string message)
        {
            if (debugLogs)
                Debug.Log($"{LogPrefix} {message}");
        }

        private void Warn(string message)
        {
            Debug.LogWarning($"{LogPrefix} {message}");
        }

        private class ActiveManipulation
        {
            public string TargetObject;
            public double StartTime;
            public int RoomId;
            public string ExtraJson;
        }
    }
}