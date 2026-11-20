using System.Globalization;
using Telemetry.SceneState;
using Telemetry.Session;
using Telemetry.Writers;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class GazeDwellManagerAnchor : MonoBehaviour
{
    private const string LogPrefix = "[GAZE_DWELL_ANCHOR]";
    private const string EventTypeGazeFixation = "GAZE_FIXATION";

    [Header("Telemetry")]
    [SerializeField] private EventLogCsvWriter eventsWriter;
    [SerializeField] private RoomStateProvider roomStateProvider;

    [Header("Gaze Source")]
    [SerializeField] private XRGazeInteractor gazeInteractor;

    [Tooltip("Opcjonalne Ÿród³o raycastu, np. Gaze Stabilized. Jeœli puste, u¿yje gazeInteractor.")]
    [SerializeField] private Transform rayOriginOverride;

    [Header("Raycast")]
    [Tooltip("Warstwy obiektów analizowanych jako cele spojrzenia, np. GazeTarget.")]
    [SerializeField] private LayerMask gazeLayerMask;

    [Tooltip("Warstwy triggerów, które mimo Is Trigger maj¹ byæ traktowane jako cele gaze, np. GazeTarget.")]
    [SerializeField] private LayerMask gazeTriggerLayerMask;

    [SerializeField] private float maxDistance = 30f;

    [Tooltip("Minimalny czas patrzenia, ¿eby zapisaæ event. 0.10 = 100 ms.")]
    [SerializeField] private float minFixationDurationSeconds = 0.10f;

    [Tooltip("Jeœli true, u¿ywa XRGazeInteractor.TryGetCurrent3DRaycastHit przed fallbackowym Physics.Raycast.")]
    [SerializeField] private bool preferXriGazeHit = true;

    [Tooltip("Jeœli true, obiekt bez komponentu GazeTarget te¿ zostanie zapisany po nazwie GameObjectu.")]
    [SerializeField] private bool allowFallbackToGameObjectName = true;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    [SerializeField] private bool enableStateLogs = false;
    [SerializeField] private float stateLogInterval = 1.0f;

    [Header("Runtime State")]
    [SerializeField] private string currentTargetId;
    [SerializeField] private string currentHitObjectName;
    [SerializeField] private string currentIdSource;
    [SerializeField] private double currentEnterTime;
    [SerializeField] private int loggedFixations;
    [SerializeField] private bool hasCurrentHit;

    private float nextStateLogTime;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();

        currentTargetId = null;
        currentHitObjectName = null;
        currentIdSource = null;
        currentEnterTime = 0d;

        Log("Enabled.");
    }

    private void OnDisable()
    {
        EndCurrentFixationIfAny();
        Log("Disabled.");
    }

    private void Update()
    {
        ResolveReferences();

        if (!TryGetSessionTime(out double now))
        {
            ClearCurrentTarget();
            return;
        }

        GazeHitData hitData;
        bool hasHit = TryResolveCurrentGazeTarget(out hitData);

        hasCurrentHit = hasHit;

        string newTargetId = hasHit ? hitData.TargetId : null;

        if (newTargetId != currentTargetId)
        {
            EndCurrentFixationIfAny();

            if (hasHit)
                StartNewFixation(hitData, now);
            else
                ClearCurrentTarget();
        }

        PeriodicStateLog();
    }

    private void ResolveReferences()
    {
        if (eventsWriter == null)
            eventsWriter = FindObjectOfType<EventLogCsvWriter>();

        if (roomStateProvider == null)
            roomStateProvider = FindObjectOfType<RoomStateProvider>();
    }

    private void StartNewFixation(GazeHitData hitData, double enterTime)
    {
        currentTargetId = hitData.TargetId;
        currentHitObjectName = hitData.HitObjectName;
        currentIdSource = hitData.IdSource;
        currentEnterTime = enterTime;

        Log(
            $"Start fixation | Target={currentTargetId} | " +
            $"HitObject={currentHitObjectName} | Source={currentIdSource}");
    }

    private void EndCurrentFixationIfAny()
    {
        if (string.IsNullOrEmpty(currentTargetId))
            return;

        if (!TryGetSessionTime(out double exitTime))
        {
            ClearCurrentTarget();
            return;
        }

        double duration = exitTime - currentEnterTime;

        if (duration < minFixationDurationSeconds)
        {
            Log(
                $"Ignored short fixation | Target={currentTargetId} | " +
                $"Duration={duration.ToString("F4", CultureInfo.InvariantCulture)}");

            ClearCurrentTarget();
            return;
        }

        int roomId = roomStateProvider != null
            ? roomStateProvider.CurrentRoomId
            : -1;

        string extraJson = BuildExtraJson(
            currentHitObjectName,
            currentIdSource);

        if (eventsWriter == null)
        {
            Warn($"EventLogCsvWriter not found. Fixation not written | Target={currentTargetId}");
            ClearCurrentTarget();
            return;
        }

        eventsWriter.LogEvent(
            currentEnterTime,
            exitTime,
            EventTypeGazeFixation,
            currentTargetId,
            duration,
            roomId,
            extraJson);

        loggedFixations++;

        Log(
            $"Logged fixation | Target={currentTargetId} | " +
            $"Duration={duration.ToString("F4", CultureInfo.InvariantCulture)} | Room={roomId}");

        ClearCurrentTarget();
    }

    private void ClearCurrentTarget()
    {
        currentTargetId = null;
        currentHitObjectName = null;
        currentIdSource = null;
        currentEnterTime = 0d;
        hasCurrentHit = false;
    }

    private bool TryResolveCurrentGazeTarget(out GazeHitData hitData)
    {
        hitData = default;

        RaycastHit hit;

        if (preferXriGazeHit &&
            gazeInteractor != null &&
            gazeInteractor.TryGetCurrent3DRaycastHit(out hit))
        {
            if (hit.collider != null)
            {
                GameObject hitObject = hit.collider.gameObject;

                bool isTrigger = hit.collider.isTrigger;

                if (!isTrigger)
                {
                    if (IsLayerAllowed(hitObject.layer))
                    {
                        if (TryBuildHitData(hit, out hitData))
                            return true;
                    }
                }

                else
                {
                    if (IsGazeTriggerLayerAllowed(hitObject.layer))
                    {
                        if (TryBuildHitData(hit, out hitData))
                            return true;
                    }
                }
            }
        }

        Transform origin = GetRayOrigin();

        if (origin == null)
            return false;

        RaycastHit[] hits = Physics.RaycastAll(
            origin.position,
            origin.forward,
            maxDistance,
            ~0,
            QueryTriggerInteraction.Collide
        );

        if (hits == null || hits.Length == 0)
            return false;

        System.Array.Sort(
            hits,
            (a, b) => a.distance.CompareTo(b.distance)
        );

        foreach (RaycastHit candidate in hits)
        {
            if (candidate.collider == null)
                continue;

            GameObject hitObject = candidate.collider.gameObject;

            if (candidate.collider.isTrigger)
            {
                if (!IsGazeTriggerLayerAllowed(hitObject.layer))
                    continue;

                if (TryBuildHitData(candidate, out hitData))
                    return true;

                continue;
            }
            if (!IsLayerAllowed(hitObject.layer))
                continue;

            if (TryBuildHitData(candidate, out hitData))
                return true;
        }

        return false;
    }

    private bool TryBuildHitData(RaycastHit hit, out GazeHitData hitData)
    {
        hitData = default;

        if (hit.collider == null)
            return false;

        GameObject hitObject = hit.collider.gameObject;

        GazeTarget gazeTarget = hit.collider.GetComponentInParent<GazeTarget>();

        string targetId = null;
        string idSource = null;

        if (gazeTarget != null && !string.IsNullOrWhiteSpace(gazeTarget.targetId))
        {
            targetId = gazeTarget.targetId;
            idSource = "gaze_target_component";
        }
        else if (allowFallbackToGameObjectName)
        {
            if (hitObject.name.StartsWith("__GAZE_HITBOX"))
            {
                Transform parent = hitObject.transform.parent;

                if (parent == null)
                    return false;

                targetId = parent.gameObject.name;
                idSource = "gaze_hitbox_parent";
            }
            else
            {
                targetId = hitObject.name;
                idSource = "fallback_gameobject_name";
            }
        }

        if (string.IsNullOrWhiteSpace(targetId))
            return false;

        hitData = new GazeHitData
        {
            TargetId = targetId,
            HitObjectName = hitObject.name,
            IdSource = idSource,
            Distance = hit.distance
        };

        return true;
    }

    private bool TryGetSessionTime(out double sessionTime)
    {
        sessionTime = 0d;

        TelemetrySessionManager manager = TelemetrySessionManager.Instance;

        if (manager == null)
            return false;

        if (manager.Clock == null)
            return false;

        if (!manager.Clock.IsRunning)
            return false;

        sessionTime = manager.Clock.CurrentTimeSeconds;
        return true;
    }

    private bool IsLayerAllowed(int layer)
    {
        return (gazeLayerMask.value & (1 << layer)) != 0;
    }

    private bool IsGazeTriggerLayerAllowed(int layer)
    {
        return (gazeTriggerLayerMask.value & (1 << layer)) != 0;
    }

    private Transform GetRayOrigin()
    {
        if (rayOriginOverride != null)
            return rayOriginOverride;

        if (gazeInteractor != null)
            return gazeInteractor.transform;

        return null;
    }

    private string BuildExtraJson(string hitObjectName, string idSource)
    {
        return
            "{" +
            $"\"hit_object\":\"{EscapeJson(hitObjectName)}\"," +
            $"\"id_source\":\"{EscapeJson(idSource)}\"" +
            "}";
    }

    private string EscapeJson(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"");
    }

    private void PeriodicStateLog()
    {
        if (!enableDebugLogs || !enableStateLogs)
            return;

        float safeInterval = Mathf.Max(0.25f, stateLogInterval);

        if (Time.time < nextStateLogTime)
            return;

        nextStateLogTime = Time.time + safeInterval;

        Log(
            $"State | HasHit={hasCurrentHit} | " +
            $"CurrentTarget={currentTargetId ?? "NULL"} | " +
            $"HitObject={currentHitObjectName ?? "NULL"} | " +
            $"IdSource={currentIdSource ?? "NULL"} | " +
            $"LoggedFixations={loggedFixations} | " +
            $"Room={(roomStateProvider != null ? roomStateProvider.CurrentRoomId.ToString() : "NULL")} | " +
            $"Writer={(eventsWriter != null ? "Assigned" : "NULL")}");
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

    private struct GazeHitData
    {
        public string TargetId;
        public string HitObjectName;
        public string IdSource;
        public float Distance;
    }
}