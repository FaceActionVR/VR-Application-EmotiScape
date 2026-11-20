using System;
using System.Collections.Generic;
using System.IO;
using Telemetry.Models;
using Telemetry.Sampling;
using Telemetry.Session;
using Telemetry.Spatial;
using UnityEngine;

namespace Telemetry.Metadata
{
    public class SessionMetadataBuilder : MonoBehaviour
    {
        private const string LogPrefix = "[SessionMetadataBuilder]";

        [Header("References")]
        [SerializeField] private TelemetrySessionManager sessionManager;
        [SerializeField] private SessionPathProvider pathProvider;
        [SerializeField] private TelemetrySourceRegistry sourceRegistry;
        [SerializeField] private SpatialAnchorRegistry spatialAnchorRegistry;

        [Header("Metadata Settings")]
        [SerializeField] private string fileName = "session_meta.json";
        [SerializeField] private string schemaVersion = "1.1";

        [Header("FOV")]
        [SerializeField] private float headsetHorizontalFov = 110f;
        [SerializeField] private float headsetVerticalFov = 90f;

        [Header("Standing Height Calibration")]
        [Tooltip("Pokój, w którym wykonywana jest kalibracja wysokoœci. HUB = 0.")]
        [SerializeField] private int heightCalibrationRoomId = 0;

        [Tooltip("Ile pierwszych sekund poprawnych próbek HMD z HUB wykorzystaæ.")]
        [SerializeField] private float heightCalibrationDurationSeconds = 3f;

        [Tooltip("Poziom pod³ogi HUB w Unity WORLD.")]
        [SerializeField] private float hubFloorYWorld = 0f;

        [Tooltip("Dolna granica poprawnej wysokoœci HMD nad pod³og¹.")]
        [SerializeField] private float minimumAcceptedHeightMeters = 0.8f;

        [Tooltip("Górna granica poprawnej wysokoœci HMD nad pod³og¹.")]
        [SerializeField] private float maximumAcceptedHeightMeters = 2.4f;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private string currentFilePath;
        [SerializeField] private string startedAtUtc;
        [SerializeField] private string stoppedAtUtc;
        [SerializeField] private float capturedFaceTrackingHz = -1f;
        [SerializeField] private float capturedPhysicsTrackingHz = -1f;
        [SerializeField] private int capturedAnchorCount;
        [Header("Height Calibration Debug")]
        [SerializeField] private PhysicsTrackingSampler activePhysicsSampler;
        [SerializeField] private float capturedStandingHeadHeightMeters = -1f;
        [SerializeField] private string heightCalibrationStatus = "UNAVAILABLE";
        [SerializeField] private int capturedHeightSampleCount;

        public string FileName => fileName;

        private double sessionStartRealtime;

        private readonly List<float> heightSamples = new List<float>();

        private bool heightCaptureStarted;
        private bool heightCaptureFinished;

        private double heightCaptureStartTimestamp;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            SubscribeSession();
            SubscribeRegistry();

            RefreshCapturedSamplingRates();

            if (IsTelemetrySessionRunning())
            {
                ResetHeightCalibration();

                Log(
                    "Builder enabled during already running session. " +
                    "Height calibration initialized."
                );
            }

            if (sourceRegistry != null)
            {
                SetActivePhysicsSampler(
                    sourceRegistry.CurrentPhysicsTrackingSampler
                );
            }

            Log(
                $"Enabled | " +
                $"SessionManager={(sessionManager != null ? "OK" : "NULL")} | " +
                $"PathProvider={(pathProvider != null ? "OK" : "NULL")} | " +
                $"SourceRegistry={(sourceRegistry != null ? "OK" : "NULL")} | " +
                $"SpatialAnchorRegistry={(spatialAnchorRegistry != null ? "OK" : "NULL")} | " +
                $"PhysicsSampler={(activePhysicsSampler != null ? activePhysicsSampler.name : "NULL")}");
        }

        private void OnDisable()
        {
            SetActivePhysicsSampler(null);
            UnsubscribeRegistry();
            UnsubscribeSession();
        }

        private void ResolveReferences()
        {
            if (sessionManager == null)
                sessionManager = TelemetrySessionManager.Instance;

            if (pathProvider == null && sessionManager != null)
                pathProvider = sessionManager.GetComponent<SessionPathProvider>();

            if (sourceRegistry == null)
                sourceRegistry = TelemetrySourceRegistry.Instance;

            if (spatialAnchorRegistry == null)
                spatialAnchorRegistry = SpatialAnchorRegistry.Instance;
        }

        private void SubscribeSession()
        {
            if (sessionManager == null)
            {
                Warn("Cannot subscribe session events. SessionManager is NULL.");
                return;
            }

            sessionManager.OnSessionStarted -= HandleSessionStarted;
            sessionManager.OnSessionStopping -= HandleSessionStopping;

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
            {
                Warn("Cannot subscribe registry events. SourceRegistry is NULL.");
                return;
            }

            sourceRegistry.OnFaceTrackingSamplerChanged -= HandleFaceSamplerChanged;
            sourceRegistry.OnPhysicsTrackingSamplerChanged -= HandlePhysicsSamplerChanged;

            sourceRegistry.OnFaceTrackingSamplerChanged += HandleFaceSamplerChanged;
            sourceRegistry.OnPhysicsTrackingSamplerChanged += HandlePhysicsSamplerChanged;

            SetActivePhysicsSampler(
                sourceRegistry.CurrentPhysicsTrackingSampler
            );
        }

        private void UnsubscribeRegistry()
        {
            if (sourceRegistry == null)
                return;

            sourceRegistry.OnFaceTrackingSamplerChanged -= HandleFaceSamplerChanged;
            sourceRegistry.OnPhysicsTrackingSamplerChanged -= HandlePhysicsSamplerChanged;
        }

        private void HandleSessionStarted(string sessionId, string userId)
        {
            ResolveReferences();

            startedAtUtc = DateTime.UtcNow.ToString("o");
            stoppedAtUtc = "";
            currentFilePath = "";

            sessionStartRealtime = Time.realtimeSinceStartupAsDouble;

            capturedFaceTrackingHz = -1f;
            capturedPhysicsTrackingHz = -1f;
            capturedAnchorCount = 0;

            ResetHeightCalibration();

            if (spatialAnchorRegistry != null)
                spatialAnchorRegistry.ClearSessionAnchors();

            RefreshCapturedSamplingRates();

            if (sourceRegistry != null)
            {
                SetActivePhysicsSampler(
                    sourceRegistry.CurrentPhysicsTrackingSampler
                );
            }

            Log(
                $"Session started | " +
                $"SessionId={sessionId} | " +
                $"UserId={userId} | " +
                $"FaceHz={capturedFaceTrackingHz} | " +
                $"PhysicsHz={capturedPhysicsTrackingHz}");
        }

        private void HandleSessionStopping()
        {
            ResolveReferences();

            if (sessionManager == null || pathProvider == null)
            {
                Debug.LogError($"{LogPrefix} Missing session dependencies.");
                return;
            }

            stoppedAtUtc = DateTime.UtcNow.ToString("o");

            RefreshCapturedSamplingRates();

            if (!heightCaptureFinished)
            {
                FinalizeHeightCalibration();
            }

            capturedAnchorCount = spatialAnchorRegistry != null
                ? spatialAnchorRegistry.SessionAnchors.Count
                : 0;

            Log(
                $"Metadata snapshot before write | " +
                $"FaceHz={capturedFaceTrackingHz} | " +
                $"PhysicsHz={capturedPhysicsTrackingHz} | " +
                $"StandingHeadHeight={capturedStandingHeadHeightMeters:F3} m | " +
                $"HeightStatus={heightCalibrationStatus} | " +
                $"HeightSamples={capturedHeightSampleCount} | " +
                $"Anchors={capturedAnchorCount}");

            SessionMetadata metadata = BuildMetadata();

            WriteMetadata(metadata);
        }

        private void HandleFaceSamplerChanged(Telemetry.Sampling.FaceTrackingSampler sampler)
        {
            if (sampler == null)
                return;

            capturedFaceTrackingHz = sampler.SampleRateHz;

            Log($"Face sampler changed | Hz={capturedFaceTrackingHz}");
        }

        private void HandlePhysicsSamplerChanged(Telemetry.Sampling.PhysicsTrackingSampler sampler)
        {
            SetActivePhysicsSampler(sampler);

            if (sampler == null)
                return;

            capturedPhysicsTrackingHz = sampler.SampleRateHz;

            Log($"Physics sampler changed | Hz={capturedPhysicsTrackingHz}");
        }

        private void SetActivePhysicsSampler(
           PhysicsTrackingSampler newSampler)
        {
            if (activePhysicsSampler == newSampler)
                return;

            if (activePhysicsSampler != null)
            {
                activePhysicsSampler.OnSampleGenerated -=
                    HandlePhysicsSampleGenerated;
            }

            activePhysicsSampler = newSampler;

            if (activePhysicsSampler != null)
            {
                activePhysicsSampler.OnSampleGenerated -=
                    HandlePhysicsSampleGenerated;

                activePhysicsSampler.OnSampleGenerated +=
                    HandlePhysicsSampleGenerated;

                Log(
                    $"Connected to PhysicsTrackingSampler: " +
                    $"{activePhysicsSampler.name}"
                );
            }
            else
            {
                Log("No active PhysicsTrackingSampler.");
            }
        }

        private void ResetHeightCalibration()
        {
            heightSamples.Clear();

            heightCaptureStarted = false;
            heightCaptureFinished = false;

            heightCaptureStartTimestamp = 0.0;

            capturedStandingHeadHeightMeters = -1f;
            capturedHeightSampleCount = 0;

            heightCalibrationStatus = "WAITING_FOR_HUB";

            Log(
                $"Height calibration reset | " +
                $"Waiting for roomId={heightCalibrationRoomId}"
            );
        }

        private void HandlePhysicsSampleGenerated(
            PhysicsTrackingSample sample)
        {
            if (!IsTelemetrySessionRunning())
                return;

            if (heightCaptureFinished)
                return;

            if (sample.RoomId != heightCalibrationRoomId)
                return;

            float standingHeadHeight =
                sample.HeadPosition.y - hubFloorYWorld;

            if (float.IsNaN(standingHeadHeight) ||
                float.IsInfinity(standingHeadHeight))
            {
                return;
            }

            if (!heightCaptureStarted)
            {
                heightCaptureStarted = true;
                heightCaptureStartTimestamp = sample.Timestamp;
                heightCalibrationStatus = "CAPTURING";

                Log(
                    $"Height calibration started | " +
                    $"RoomId={sample.RoomId} | " +
                    $"StartTimestamp={sample.Timestamp:F4} | " +
                    $"FirstHeight={standingHeadHeight:F3} m"
                );
            }

            double elapsed =
                sample.Timestamp - heightCaptureStartTimestamp;

            if (elapsed < 0.0)
            {
                Warn(
                    $"Height calibration received negative elapsed time. " +
                    $"Timestamp={sample.Timestamp:F4}, " +
                    $"Start={heightCaptureStartTimestamp:F4}"
                );

                return;
            }

            if (elapsed <= heightCalibrationDurationSeconds)
            {
                heightSamples.Add(standingHeadHeight);

                return;
            }

            FinalizeHeightCalibration();
        }

        private bool IsTelemetrySessionRunning()
        {
            return sessionManager != null
                && sessionManager.Clock != null
                && sessionManager.Clock.IsRunning;
        }

        private void FinalizeHeightCalibration()
        {
            if (heightCaptureFinished)
                return;

            heightCaptureFinished = true;
            capturedHeightSampleCount = heightSamples.Count;

            if (heightSamples.Count == 0)
            {
                capturedStandingHeadHeightMeters = -1f;
                heightCalibrationStatus = "UNAVAILABLE";

                Warn(
                    "Height calibration unavailable. " +
                    "No valid HUB samples were collected."
                );

                return;
            }

            float median = CalculateMedian(heightSamples);

            if (float.IsNaN(median) ||
                float.IsInfinity(median))
            {
                capturedStandingHeadHeightMeters = -1f;
                heightCalibrationStatus = "INVALID";

                Warn(
                    "Height calibration invalid. " +
                    "Median is NaN or Infinity."
                );

                return;
            }

            if (median < minimumAcceptedHeightMeters ||
                median > maximumAcceptedHeightMeters)
            {
                capturedStandingHeadHeightMeters = -1f;
                heightCalibrationStatus = "INVALID";

                Warn(
                    $"Height calibration rejected | " +
                    $"Median={median:F3} m | " +
                    $"AllowedRange=" +
                    $"{minimumAcceptedHeightMeters:F2}-" +
                    $"{maximumAcceptedHeightMeters:F2} m | " +
                    $"Samples={heightSamples.Count}"
                );

                return;
            }

            capturedStandingHeadHeightMeters = median;
            heightCalibrationStatus = "VALID";

            Log(
                $"Standing head height captured | " +
                $"Median={capturedStandingHeadHeightMeters:F3} m | " +
                $"Samples={capturedHeightSampleCount} | " +
                $"Window={heightCalibrationDurationSeconds:F1}s | " +
                $"RoomId={heightCalibrationRoomId}"
            );
        }

        private float CalculateMedian(List<float> values)
        {
            if (values == null || values.Count == 0)
                return float.NaN;

            List<float> sortedValues = new List<float>(values);

            sortedValues.Sort();

            int count = sortedValues.Count;
            int middle = count / 2;

            if (count % 2 == 1)
            {
                return sortedValues[middle];
            }
            return (
                sortedValues[middle - 1] +
                sortedValues[middle]
            ) * 0.5f;
        }

        private void RefreshCapturedSamplingRates()
        {
            ResolveReferences();

            if (sourceRegistry == null)
                return;

            if (capturedFaceTrackingHz <= 0f && sourceRegistry.LastKnownFaceSampleRateHz > 0f)
                capturedFaceTrackingHz = sourceRegistry.LastKnownFaceSampleRateHz;

            if (capturedPhysicsTrackingHz <= 0f && sourceRegistry.LastKnownPhysicsSampleRateHz > 0f)
                capturedPhysicsTrackingHz = sourceRegistry.LastKnownPhysicsSampleRateHz;
        }

        private SessionMetadata BuildMetadata()
        {
            double duration = ResolveDurationSeconds();

            return new SessionMetadata
            {
                schema_version = schemaVersion,
                session_id = sessionManager.CurrentSessionId,
                user_id = sessionManager.CurrentUserId,
                app_version = Application.version,
                started_at_utc = startedAtUtc,
                stopped_at_utc = stoppedAtUtc,
                duration_seconds = duration,

                sampling = new SamplingMetadata
                {
                    face_tracking_hz = capturedFaceTrackingHz,
                    physics_tracking_hz = capturedPhysicsTrackingHz
                },

                fov = new FovMetadata
                {
                    horizontal = headsetHorizontalFov,
                    vertical = headsetVerticalFov
                },

                coordinate_system = new CoordinateSystemMetadata
                {
                    engine = "Unity",
                    handedness = "left",
                    units = "meters"
                },

                calibration = new CalibrationMetadata
                {
                    status =
                        heightCalibrationStatus,

                    source_room_id =
                        heightCalibrationRoomId,

                    standing_head_height_m =
                        capturedStandingHeadHeightMeters,

                    hub_floor_y_world =
                        hubFloorYWorld,

                    sample_count =
                        capturedHeightSampleCount,

                    window_seconds =
                        heightCalibrationDurationSeconds,

                    method =
                        "MEDIAN_HMD_Y_MINUS_FLOOR_Y"
                },

                anchors = BuildAnchorMetadata()
            };
        }

        private double ResolveDurationSeconds()
        {
            if (sessionManager != null &&
                sessionManager.Clock != null)
            {
                return sessionManager.Clock.CurrentTimeSeconds;
            }

            return Time.realtimeSinceStartupAsDouble - sessionStartRealtime;
        }

        private List<AnchorMetadata> BuildAnchorMetadata()
        {
            var result = new List<AnchorMetadata>();

            if (spatialAnchorRegistry == null)
            {
                Warn("SpatialAnchorRegistry is NULL. No anchors written.");
                return result;
            }

            foreach (var anchor in spatialAnchorRegistry.SessionAnchors)
            {
                if (anchor == null)
                    continue;

                result.Add(new AnchorMetadata
                {
                    room_id = anchor.room_id,
                    anchor_name = anchor.anchor_name,
                    position = anchor.position,
                    rotation = anchor.rotation,
                    scene_name = anchor.scene_name
                });
            }

            return result;
        }

        private void WriteMetadata(SessionMetadata metadata)
        {
            currentFilePath = pathProvider.GetFilePath(fileName);

            if (string.IsNullOrWhiteSpace(currentFilePath))
            {
                Debug.LogError($"{LogPrefix} Could not resolve metadata path.");
                return;
            }

            string json = JsonUtility.ToJson(metadata, true);

            try
            {
                File.WriteAllText(currentFilePath, json);

                Log(
                    $"Metadata written: {currentFilePath} | " +
                    $"FaceHz={metadata.sampling.face_tracking_hz} | " +
                    $"PhysicsHz={metadata.sampling.physics_tracking_hz} | " +
                    $"HeightStatus={metadata.calibration.status} | " +
                    $"StandingHeadHeight=" +
                    $"{metadata.calibration.standing_head_height_m:F3} m | " +
                    $"HeightSamples=" +
                    $"{metadata.calibration.sample_count} | " +
                    $"Anchors={metadata.anchors.Count}");
            }
            catch (Exception e)
            {
                Debug.LogError($"{LogPrefix} Failed to write metadata:\n{e}");
            }
        }

        private void Log(string message)
        {
            if (!enableDebugLogs)
                return;

            Debug.Log($"{LogPrefix} {message}");
        }

        private void Warn(string message)
        {
            if (!enableDebugLogs)
                return;

            Debug.LogWarning($"{LogPrefix} {message}");
        }

        [Serializable]
        private class SessionMetadata
        {
            public string schema_version;
            public string session_id;
            public string user_id;
            public string app_version;
            public string started_at_utc;
            public string stopped_at_utc;
            public double duration_seconds;

            public SamplingMetadata sampling;
            public FovMetadata fov;
            public CoordinateSystemMetadata coordinate_system;

            public CalibrationMetadata calibration;

            public List<AnchorMetadata> anchors;
        }

        [Serializable]
        private class SamplingMetadata
        {
            public float face_tracking_hz;
            public float physics_tracking_hz;
        }

        [Serializable]
        private class FovMetadata
        {
            public float horizontal;
            public float vertical;
        }

        [Serializable]
        private class CoordinateSystemMetadata
        {
            public string engine;
            public string handedness;
            public string units;
        }

        [Serializable]
        private class CalibrationMetadata
        {
            public string status;
            public int source_room_id;
            public float standing_head_height_m;
            public float hub_floor_y_world;
            public int sample_count;
            public float window_seconds;
            public string method;
        }

        [Serializable]
        private class AnchorMetadata
        {
            public int room_id;
            public string anchor_name;
            public Vector3 position;
            public Quaternion rotation;
            public string scene_name;
        }
    }
}