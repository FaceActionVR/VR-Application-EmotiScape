using System;
using System.Collections.Generic;
using Telemetry.SceneState;
using Telemetry.Writers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors; // W nowszych wersjach XRI moøe byÊ wymagane

public class GazeDwellManagerNew : MonoBehaviour
{
    [Header("Telemetry")]
    [SerializeField] private EventLogCsvWriter eventsWriter;
    [SerializeField] private RoomStateProvider roomStateProvider;

    [Header("èrÛd≥a")]
    public XRGazeInteractor gazeInteractor;
    public Transform rayOriginOverride;

    [Header("Filtry / Raycast")]
    public LayerMask gazeLayerMask;
    public float maxDistance = 30f;
    public float minSampleDuration = 0.05f;

    [Header("Cele")]
    public List<GazeTarget> trackedTargets = new List<GazeTarget>();

    [Header("UI")]
    public List<TMP_Text> uiCellsTMP = new List<TMP_Text>();
    public List<Text> uiCellsUGUI = new List<Text>();
    public float uiUpdateInterval = 0.1f;

    // stan
   
    [Header("Debug")]
    [SerializeField] private string currentId;
    [SerializeField] private double enterTime;
    [SerializeField] private int loggedFixations;

    private float uiTimer;
    private readonly Dictionary<string, float> dwellById = new Dictionary<string, float>();

    private const string EventTypeGazeFixation = "GAZE_FIXATION";

    void Awake()
    {
        ResolveReferences();
        InitializeTrackedTargets();
    }

    private void OnEnable()
    {
        ResolveReferences();
        enterTime = GetSessionTime();
    }

    private void OnDisable()
    {
        if (!string.IsNullOrEmpty(currentId))
        {
            HandleGazeChange(currentId, null);
        }
    }
    private void ResolveReferences()
    {
        if (eventsWriter == null)
            eventsWriter = FindObjectOfType<EventLogCsvWriter>();

        if (roomStateProvider == null)
            roomStateProvider = FindObjectOfType<RoomStateProvider>();
    }

    private void InitializeTrackedTargets()
    {
        foreach (var target in trackedTargets)
        {
            if (target != null && !dwellById.ContainsKey(target.targetId))
                dwellById.Add(target.targetId, 0f);
        }
    }

    void Update()
    {
        var hasHit = TryGetHit(out RaycastHit hit);

        string newId = null;
        if (hasHit)
        {
            // 1. Szukamy skryptu (Priorytet)
            var gt = hit.collider.GetComponentInParent<GazeTarget>();

            // Sprawdzamy, czy skrypt istnieje I czy ma wpisane jakieú ID
            if (gt != null && !string.IsNullOrEmpty(gt.targetId))
            {
                newId = gt.targetId;
            }
            else
            {
                // 2. Brak skryptu -> Bierzemy nazwÍ obiektu (Fallback)
                newId = hit.collider.gameObject.name;
            }
        }

        // Zmiana celu?
        if (newId != currentId)
        {
            // Zanim zmienimy currentId, zamykamy i LOGUJEMY poprzedniπ sesjÍ
            HandleGazeChange(currentId, newId);

            currentId = newId;
            enterTime = GetSessionTime(); // Resetujemy czas wejúcia dla nowego obiektu
        }

        // UI - bez zmian
        uiTimer += Time.deltaTime;
        if (uiTimer >= uiUpdateInterval)
        {
            uiTimer = 0f;
            RefreshUI();
        }
    }

    // --- NOWA METODA DO OBS£UGI LOGIKI ZMIANY ---
    void HandleGazeChange(string oldTarget, string newTarget)
    {
        // Jeúli wczeúniej patrzyliúmy na coú (oldTarget nie jest null)
        if (string.IsNullOrEmpty(oldTarget))
            return;
        double exitTime = Time.timeAsDouble;
        double duration = exitTime - enterTime;

            // Sprawdzamy czy to nie by≥ tylko "mikro-rzut" okiem (filtr szumÛw)
        if (duration < minSampleDuration)
        {
            return;
        }
            
                // 1. Aktualizujemy sumaryczny czas dla UI
        if (!dwellById.ContainsKey(oldTarget))
            dwellById[oldTarget] = 0f;
        dwellById[oldTarget] += (float)duration;

        int roomId = roomStateProvider != null ? roomStateProvider.CurrentRoomId : -1;
        eventsWriter?.LogEvent(
            enterTime,
            exitTime,
            EventTypeGazeFixation,
            oldTarget,
            duration,
            roomId,
            "{}"
        );

        loggedFixations++;
    }

    private double GetSessionTime()
    {
        var manager = Telemetry.Session.TelemetrySessionManager.Instance;

        if (manager != null && manager.Clock != null && manager.Clock.IsRunning)
            return manager.Clock.CurrentTimeSeconds;

        return Time.timeAsDouble;
    }

    bool TryGetHit(out RaycastHit hit)
    {
        if (gazeInteractor != null && gazeInteractor.TryGetCurrent3DRaycastHit(out hit))
        {
            if (((1 << hit.collider.gameObject.layer) & gazeLayerMask) != 0)
                return true;
        }

        var origin = GetRayOrigin();

        if (Physics.Raycast(origin.position, origin.forward, out hit, maxDistance, gazeLayerMask, QueryTriggerInteraction.Ignore))
            return true;

        return false;
    }

    Transform GetRayOrigin()
    {
        if (rayOriginOverride) return rayOriginOverride;
        if (gazeInteractor) return gazeInteractor.transform;
        return Camera.main ? Camera.main.transform : transform;
    }

    void RefreshUI()
    {
        // (Twoja stara logika UI tutaj... skrÛci≥em dla czytelnoúci)
        if (uiCellsTMP != null && uiCellsTMP.Count > 0)
        {
            for (int i = 0; i < uiCellsTMP.Count; i++)
            {
                var label = uiCellsTMP[i];
                if (!label) continue;
                string id = ResolveIdByIndex(i);
                float sec = dwellById.ContainsKey(id) ? dwellById[id] : 0f;
                label.text = $"{id}: {sec:0.00} s";
            }
        }
    }

    string ResolveIdByIndex(int index)
    {
        if (index >= 0 && index < trackedTargets.Count && trackedTargets[index] != null)
            return trackedTargets[index].targetId;
        return $"Target+{index + 1}";
    }

    public void ResetTimes()
    {
        // Przy resecie, jeúli na coú patrzymy, warto to "domknπÊ" w logach
        if (!string.IsNullOrEmpty(currentId))
        {
            HandleGazeChange(currentId, null);
        }

        var keys = new List<string>(dwellById.Keys);
        foreach (var k in keys) dwellById[k] = 0f;
        currentId = null;
        enterTime = Time.timeAsDouble;
        RefreshUI();
    }
}