using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit;
using TMPro;                 // jeśli używasz TextMeshPro
using UnityEngine.UI;        // jeśli używasz klasycznego Text

public class GazeDwellManager : MonoBehaviour
{
    [Header("Źródła")]
    public XRGazeInteractor gazeInteractor;   // przeciągnij swój Gaze Interactor
    public Transform rayOriginOverride;       // (opcjonalnie) np. Gaze Stabilized

    [Header("Filtry / Raycast")]
    public LayerMask gazeLayerMask;           // ustaw na GazeTargets
    public float maxDistance = 30f;
    public float minSampleDuration = 0.05f;   // ignoruj króciutkie „pstryki”

    [Header("Cele (opcjonalnie, dla porządku)")]
    public List<GazeTarget> trackedTargets = new List<GazeTarget>(); // 9 Cubów (nie wymagane, ale porządkuje)

    [Header("UI (9 pól)")]
    public List<TMP_Text> uiCellsTMP = new List<TMP_Text>(); // wypełnij 9 referencji
    public List<Text> uiCellsUGUI = new List<Text>();        // alternatywa, jeśli nie używasz TMP
    public float uiUpdateInterval = 0.1f;

    // stan
    private readonly Dictionary<string, float> dwellById = new(); // sekundy
    private string currentId;
    private double enterTime;
    private float uiTimer;

    void Awake()
    {
        // inicjalizacja słownika z góry (ładne zerowanie i stabilny porządek)
        foreach (var t in trackedTargets)
        {
            if (t && !dwellById.ContainsKey(t.targetId))
                dwellById.Add(t.targetId, 0f);
        }
    }

    void Update()
    {
        // 1) Ustal bieżący hit (najpierw XRI, w razie czego Physics)
        var hasHit = TryGetHit(out RaycastHit hit);

        // 2) Sprawdź, czy trafiliśmy obiekt z GazeTarget
        string newId = null;
        if (hasHit)
        {
            var gt = hit.collider.GetComponentInParent<GazeTarget>();
            if (gt != null)
                newId = gt.targetId;
            else
                newId = null; // trafiony obiekt nie jest celem – nie licz
        }

        // 3) Zmiana celu? zamknij poprzednią próbkę i otwórz nową
        if (newId != currentId)
        {
            EndSample();
            currentId = newId;
            enterTime = Time.timeAsDouble;
        }

        // 4) UI – odświeżaj rzadziej niż co frame
        uiTimer += Time.deltaTime;
        if (uiTimer >= uiUpdateInterval)
        {
            uiTimer = 0f;
            RefreshUI();
        }
    }

    bool TryGetHit(out RaycastHit hit)
    {
        // a) XRI – jeśli Gaze Interactor ma trafienie 3D
        if (gazeInteractor != null && gazeInteractor.TryGetCurrent3DRaycastHit(out hit))
        {
            // filtr warstw: gdy XRI dotknął obiekt nie z naszej maski, zignoruj
            if (((1 << hit.collider.gameObject.layer) & gazeLayerMask) != 0)
                return true;
        }

        // b) Fallback – Physics.Raycast z naszego źródła
        var origin = GetRayOrigin();
        if (Physics.Raycast(origin.position, origin.forward, out hit, maxDistance, gazeLayerMask,
            QueryTriggerInteraction.Ignore))
            return true;

        return false;
    }

    Transform GetRayOrigin()
    {
        if (rayOriginOverride) return rayOriginOverride;         // np. Gaze Stabilized
        if (gazeInteractor) return gazeInteractor.transform;     // obiekt interaktora
        return Camera.main ? Camera.main.transform : transform;  // awaryjnie
    }

    void EndSample()
    {
        if (string.IsNullOrEmpty(currentId)) return;

        double dt = Time.timeAsDouble - enterTime;
        if (dt >= minSampleDuration)
        {
            if (!dwellById.ContainsKey(currentId))
                dwellById[currentId] = 0f;

            dwellById[currentId] += (float)dt;
        }
    }

    void RefreshUI()
    {
        // Priorytet: TMP (jeśli ustawione), inaczej klasyczne Text
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
            return;
        }

        if (uiCellsUGUI != null && uiCellsUGUI.Count > 0)
        {
            for (int i = 0; i < uiCellsUGUI.Count; i++)
            {
                var label = uiCellsUGUI[i];
                if (!label) continue;

                string id = ResolveIdByIndex(i);
                float sec = dwellById.ContainsKey(id) ? dwellById[id] : 0f;
                label.text = $"{id}: {sec:0.00} s";
            }
        }
    }

    // Mapowanie: indeks pola → id
    // Najprościej: jeśli podałeś trackedTargets w kolejności, użyj ich.
    string ResolveIdByIndex(int index)
    {
        if (index >= 0 && index < trackedTargets.Count && trackedTargets[index] != null)
            return trackedTargets[index].targetId;

        // Gdy brak listy – spróbuj domyślne nazwy Cube1..Cube9
        return $"Cube{index + 1}";
    }

    // Publiczny reset (podpinamy pod przycisk)
    public void ResetTimes()
    {
        EndSample();                 // domknij ewentualną próbkę
        var keys = new List<string>(dwellById.Keys);
        foreach (var k in keys) dwellById[k] = 0f;
        currentId = null;
        enterTime = Time.timeAsDouble;
        RefreshUI();
    }
}

