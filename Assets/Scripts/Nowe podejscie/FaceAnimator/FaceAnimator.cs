using UnityEngine;
using VIVE.OpenXR.FacialTracking;
using System;
using System.Collections.Generic; // Dodane dla Dictionary
using System.Linq;
using Telemetry.DataReaders;

public class FaceAnimator : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Przeci¹gnij tutaj obiekt z komponentem SkinnedMeshRenderer (np. g³owê Avatara).")]
    public SkinnedMeshRenderer targetMesh;

    [Tooltip("Czy animowaæ oczy?")]
    public bool animateEyes = true;
    [Tooltip("Czy animowaæ usta?")]
    public bool animateLips = true;

    // Mapowanie: Indeks w tablicy OpenXR -> Indeks BlendShape w MeshRendererze
    private int[] eyeShapeIndices;
    private int[] lipShapeIndices;

    private Dictionary<string, int> meshBlendShapeMap;

    void Start()
    {
        if (targetMesh == null) targetMesh = GetComponent<SkinnedMeshRenderer>();

        if (targetMesh == null)
        {
            Debug.LogError($"[FaceAnimator] Brak SkinnedMeshRenderer na obiekcie {name}!");
            enabled = false;
            return;
        }

        // 1. Najpierw "przeczytaj" model i stwórz mapê jego kszta³tów
        CacheMeshBlendShapes();

        // 2. Nastêpnie dopasuj Enumy VIVE do tej mapy
        MapBlendShapes();
    }

    void Update()
    {
        float[] eyeData = FaceReader.Eyes;
        float[] lipData = FaceReader.Lips;

        // Aplikujemy wagi tylko jeœli mamy dane i mapowanie
        if (eyeData != null && animateEyes && eyeShapeIndices != null)
            ApplyWeights(eyeData, eyeShapeIndices);

        if (lipData != null && animateLips && lipShapeIndices != null)
            ApplyWeights(lipData, lipShapeIndices);
    }

    private void ApplyWeights(float[] trackingData, int[] mapping)
    {
        for (int i = 0; i < trackingData.Length; i++)
        {
            // Zabezpieczenie przed wyjœciem poza tablicê
            if (i >= mapping.Length) break;

            int index = mapping[i];
            if (index != -1)
            {
                // Waga: OpenXR daje 0..1, Unity chce 0..100
                targetMesh.SetBlendShapeWeight(index, trackingData[i] * 100f);
            }
        }
    }

    // --- LOGIKA NORMALIZACJI (T£UMACZ) ---

    private void CacheMeshBlendShapes()
    {
        meshBlendShapeMap = new Dictionary<string, int>();
        Mesh mesh = targetMesh.sharedMesh;

        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            string rawName = mesh.GetBlendShapeName(i);

            // T³umaczenie nazwy modelu na "wspólny jêzyk":
            // 1. Usuñ prefiks "Eye_" (bo w Enumach go nie ma w œrodku nazwy)
            // 2. Usuñ podkreœlniki
            // 3. Zmieñ na ma³e litery
            // Np. "Eye_Left_Blink" -> "Left_Blink" -> "leftblink"

            string cleanName = rawName;
            if (cleanName.StartsWith("Eye_")) cleanName = cleanName.Substring(4); // Usuwa "Eye_"

            cleanName = cleanName.ToLower().Replace("_", "").Replace(" ", "");

            if (!meshBlendShapeMap.ContainsKey(cleanName))
            {
                meshBlendShapeMap.Add(cleanName, i);
            }
        }
    }

    private void MapBlendShapes()
    {
        // Listy do zbierania nazw, których nie uda siê znaleŸæ
        List<string> missingEyes = new List<string>();
        List<string> missingLips = new List<string>();

        // --- OCZY ---
        var eyeEnums = Enum.GetNames(typeof(XrEyeExpressionHTC));
        eyeShapeIndices = new int[eyeEnums.Length];
        for (int i = 0; i < eyeEnums.Length; i++)
        {
            //Ingorujemy MAX_ENUM
            if (eyeEnums[i].Contains("MAX_ENUM"))
            {
                eyeShapeIndices[i] = -1;
                continue;
            }

            int foundIndex = SmartFind(eyeEnums[i], true);
            eyeShapeIndices[i] = foundIndex;

            if (foundIndex == -1)
            {
                missingEyes.Add(eyeEnums[i]);
            }
        }

        // --- USTA ---
        var lipEnums = Enum.GetNames(typeof(XrLipExpressionHTC));
        lipShapeIndices = new int[lipEnums.Length];
        for (int i = 0; i < lipEnums.Length; i++)
        {
            if (lipEnums[i].Contains("MAX_ENUM"))
            {
                lipShapeIndices[i] = -1;
                continue;
            }

            int foundIndex = SmartFind(lipEnums[i], false);
            lipShapeIndices[i] = foundIndex;

            if (foundIndex == -1)
            {
                missingLips.Add(lipEnums[i]);
            }
        }

        // Log diagnostyczny
        int foundEyesCount = eyeShapeIndices.Count(x => x != -1);
        int foundLipsCount = lipShapeIndices.Count(x => x != -1);

        // 1. Sukcesy na zielono
        Debug.Log($"<color=green>[FaceAnimator] SUKCES! Zmapowano: Oczy {foundEyesCount}/{eyeEnums.Length}, Usta {foundLipsCount}/{lipEnums.Length}</color>");

        // 2. Braki na ¿ó³to (tylko jeœli jakieœ s¹)
        if (missingEyes.Count > 0)
        {
            Debug.LogWarning($"[FaceAnimator] BRAKUJ¥CE OCZY ({missingEyes.Count}): " + string.Join(", ", missingEyes));
        }

        if (missingLips.Count > 0)
        {
            Debug.LogWarning($"[FaceAnimator] BRAKUJ¥CE USTA ({missingLips.Count}): " + string.Join(", ", missingLips));
        }
    }

    private int SmartFind(string enumName, bool isEye)
    {
        // Enumy w OpenXR s¹ "krzycz¹ce", np: "XR_EYE_EXPRESSION_LEFT_BLINK_HTC"
        // Musimy to wyczyœciæ do postaci "leftblink"

        string cleanEnum = enumName.ToLower();

        // 1. Usuñ standardowe prefiksy OpenXR
        cleanEnum = cleanEnum.Replace("xr_eye_expression_", "");
        cleanEnum = cleanEnum.Replace("xr_lip_expression_", "");
        cleanEnum = cleanEnum.Replace("xr_facial_tracking_", "");

        // 2. Usuñ sufiks HTC
        cleanEnum = cleanEnum.Replace("_htc", "");

        // 3. Usuñ podkreœlniki
        cleanEnum = cleanEnum.Replace("_", "");

        if (isEye)
        {
            switch (cleanEnum)
            {
                case "leftout": return TryGetIndex("leftleft");   // Lewe oko w lewo (na zewn¹trz)
                case "leftin": return TryGetIndex("leftright");  // Lewe oko w prawo (do nosa)
                case "rightout": return TryGetIndex("rightright"); // Prawe oko w prawo (na zewn¹trz)
                case "rightin": return TryGetIndex("rightleft");  // Prawe oko w lewo (do nosa)
            }
        }

        // Teraz mamy np. "leftblink". Szukamy tego w s³owniku modelu.
        return TryGetIndex(cleanEnum);
    }

    private int TryGetIndex(string key)
    {
        if (meshBlendShapeMap.ContainsKey(key))
            return meshBlendShapeMap[key];
        return -1;
    }
}
