using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors; // W nowszych wersjach XRI mo¿e byæ wymagane
using UnityEngine.XR.Interaction.Toolkit;
using TMPro;
using UnityEngine.UI;

public class GazeDwellManagerGemini : MonoBehaviour
{
    [Header("Logowanie")]
    public GazeLogger gazeLogger; // <--- Przeci¹gnij tu nowy skrypt GazeLogger w Inspektorze!

    [Header("Konfiguracja Pokoju")]
    [Tooltip("Wpisz tutaj numer pokoju: 1, 2, 3 lub 4")]
    public int roomId = 1;

    [Header("ród³a")]
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
    private readonly Dictionary<string, float> dwellById = new Dictionary<string, float>();
    private string currentId;
    private double enterTime;
    private float uiTimer;

    void Awake()
    {
        foreach (var t in trackedTargets)
        {
            if (t && !dwellById.ContainsKey(t.targetId))
                dwellById.Add(t.targetId, 0f);
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

            // Sprawdzamy, czy skrypt istnieje I czy ma wpisane jakieœ ID
            if (gt != null && !string.IsNullOrEmpty(gt.targetId))
            {
                newId = gt.targetId;
            }
            else
            {
                // 2. Brak skryptu -> Bierzemy nazwê obiektu (Fallback)
                newId = hit.collider.gameObject.name;
            }
        }

        // Zmiana celu?
        if (newId != currentId)
        {
            // Zanim zmienimy currentId, zamykamy i LOGUJEMY poprzedni¹ sesjê
            HandleGazeChange(currentId, newId);

            currentId = newId;
            enterTime = Time.timeAsDouble; // Resetujemy czas wejœcia dla nowego obiektu
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
        // Jeœli wczeœniej patrzyliœmy na coœ (oldTarget nie jest null)
        if (!string.IsNullOrEmpty(oldTarget))
        {
            double exitTime = Time.timeAsDouble;
            double duration = exitTime - enterTime;

            // Sprawdzamy czy to nie by³ tylko "mikro-rzut" okiem (filtr szumów)
            if (duration >= minSampleDuration)
            {
                // 1. Aktualizujemy sumaryczny czas dla UI
                if (!dwellById.ContainsKey(oldTarget))
                    dwellById[oldTarget] = 0f;
                dwellById[oldTarget] += (float)duration;

                // 2. LOGUJEMY zdarzenie do pliku (Timeline)
                if (gazeLogger != null)
                {
                    // Zapisz: Co to by³o, kiedy zaczêliœmy, kiedy skoñczyliœmy
                    gazeLogger.LogGazeSession(oldTarget, enterTime, exitTime, roomId);
                }
            }
        }
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
        // (Twoja stara logika UI tutaj... skróci³em dla czytelnoœci)
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
        return $"Cube{index + 1}";
    }

    public void ResetTimes()
    {
        // Przy resecie, jeœli na coœ patrzymy, warto to "domkn¹æ" w logach
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