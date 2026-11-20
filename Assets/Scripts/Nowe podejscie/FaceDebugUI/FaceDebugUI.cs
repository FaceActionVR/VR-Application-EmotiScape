using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro; // <--- Kluczowa biblioteka dla TextMeshPro
using VIVE.OpenXR.FacialTracking;
using Telemetry.DataReaders;

public class FaceDebugUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Prefab z komponentem TextMeshProUGUI.")]
    public GameObject textPrefab;

    [Tooltip("Kontener (np. Panel z VerticalLayoutGroup) dla oczu.")]
    public Transform eyeContainer;

    [Tooltip("Kontener (np. Panel z VerticalLayoutGroup) dla ust.")]
    public Transform lipContainer;

    // Struktura pomocnicza
    private class DebugItem
    {
        public TMP_Text uiText; // <--- Zmiana z Text na TMP_Text
        public int index;
        public bool isEye;
        public string name;
    }

    private List<DebugItem> items = new List<DebugItem>();

    void Start()
    {
        if (textPrefab == null || eyeContainer == null || lipContainer == null)
        {
            Debug.LogError("Przypisz referencje w inspektorze!");
            return;
        }

        GenerateUI();
    }

    void GenerateUI()
    {
        // 1. Generowanie listy dla OCZU
        var eyeEnums = Enum.GetValues(typeof(XrEyeExpressionHTC));
        foreach (XrEyeExpressionHTC eyeExp in eyeEnums)
        {
            CreateDebugItem(eyeExp.ToString(), (int)eyeExp, true, eyeContainer);
        }

        // 2. Generowanie listy dla UST
        var lipEnums = Enum.GetValues(typeof(XrLipExpressionHTC));
        foreach (XrLipExpressionHTC lipExp in lipEnums)
        {
            CreateDebugItem(lipExp.ToString(), (int)lipExp, false, lipContainer);
        }
    }

    void CreateDebugItem(string name, int index, bool isEye, Transform parent)
    {
        GameObject go = Instantiate(textPrefab, parent);
        go.name = name;

        // Pobieramy komponent TMP_Text (dzia³a dla wersji UI i WorldSpace)
        TMP_Text txt = go.GetComponent<TMP_Text>();

        if (txt == null)
        {
            Debug.LogError($"Prefab '{go.name}' nie ma komponentu TextMeshPro!");
            return;
        }

        items.Add(new DebugItem
        {
            uiText = txt,
            index = index,
            isEye = isEye,
            name = name
        });
    }

    void Update()
    {
        float[] eyes = FaceReader.Eyes;
        float[] lips = FaceReader.Lips;

        if (eyes == null || lips == null) return;

        foreach (var item in items)
        {
            float value = 0f;

            if (item.isEye)
            {
                if (item.index < eyes.Length) value = eyes[item.index];
            }
            else
            {
                if (item.index < lips.Length) value = lips[item.index];
            }

            // Aktualizacja tekstu TMP
            item.uiText.text = $"{item.name}: {value:F2}";

            // Kolorowanie aktywnego parametru
            if (value > 0.05f)
                item.uiText.color = Color.green;
            else
                item.uiText.color = Color.white;
        }
    }
}