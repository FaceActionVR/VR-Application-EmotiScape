#if UNITY_EDITOR

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class AssignGlbNames
{
    [MenuItem("Tools/GLB/Numeruj powtarzaj¹ce siê nazwy w ca³ej scenie")]
    private static void NumberDuplicateNamesInActiveScene()
    {
        // ZABEZPIECZENIE 1: Zablokowanie w Play Mode
        if (Application.isPlaying)
        {
            Debug.LogWarning("[GLB] WyjdŸ z trybu Play, aby u¿yæ tego narzêdzia. Zmiany i tak nie zosta³yby zapisane.");
            return;
        }

        Scene activeScene = SceneManager.GetActiveScene();

        if (!activeScene.IsValid() || !activeScene.isLoaded)
        {
            Debug.LogError("[GLB] Aktywna scena jest nieprawid³owa lub nieza³adowana.");
            return;
        }

        GameObject[] rootObjects = activeScene.GetRootGameObjects();
        List<Transform> allSceneTransforms = new List<Transform>();

        foreach (GameObject root in rootObjects)
        {
            allSceneTransforms.AddRange(root.GetComponentsInChildren<Transform>(true));
        }

        HashSet<string> existingNames = new HashSet<string>();
        Dictionary<string, List<Transform>> duplicatesGroup = new Dictionary<string, List<Transform>>();

        foreach (Transform t in allSceneTransforms)
        {
            // ZABEZPIECZENIE 2: Ignoruj obiekty ukryte i systemowe (generowane przez Unity w tle)
            if ((t.gameObject.hideFlags & HideFlags.HideInHierarchy) != 0 ||
                (t.gameObject.hideFlags & HideFlags.NotEditable) != 0)
            {
                continue;
            }

            existingNames.Add(t.name);

            if (!duplicatesGroup.TryGetValue(t.name, out List<Transform> list))
            {
                list = new List<Transform>();
                duplicatesGroup.Add(t.name, list);
            }
            list.Add(t);
        }

        int renamedCount = 0;
        int groupCount = 0;

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Numeruj powtórzone nazwy w scenie");
        int undoGroupIndex = Undo.GetCurrentGroup();

        foreach (var entry in duplicatesGroup)
        {
            List<Transform> list = entry.Value;
            if (list.Count <= 1)
                continue;

            groupCount++;
            string baseName = entry.Key;
            int counter = 1;

            // ZABEZPIECZENIE 3: Zaczynamy pêtlê, ale pierwszy obiekt (i = 0) zostawiamy w spokoju.
            // Bêdzie on traktowany jako "orygina³". Zmieniamy tylko faktyczne duplikaty.
            for (int i = 1; i < list.Count; i++)
            {
                Transform current = list[i];
                string candidateName;

                do
                {
                    candidateName = $"{baseName}_{counter}";
                    counter++;
                }
                while (existingNames.Contains(candidateName));

                existingNames.Add(candidateName);

                Undo.RecordObject(current.gameObject, "GLB Rename Duplicate");

                current.name = candidateName;
                EditorUtility.SetDirty(current.gameObject);

                // ZABEZPIECZENIE 4: Rejestracja modyfikacji dla prefabów
                // Jeœli obiekt jest czêœci¹ instancji prefaba, ta linijka upewni siê, 
                // ¿e nazwa zostanie poprawnie nadpisana (nadpisanie w³aœciwoœci prefaba).
                PrefabUtility.RecordPrefabInstancePropertyModifications(current.gameObject);

                renamedCount++;
            }
        }

        Undo.CollapseUndoOperations(undoGroupIndex);

        EditorSceneManager.MarkSceneDirty(activeScene);

        Debug.Log($"[GLB] Gotowe dla sceny '{activeScene.name}'. " +
                  $"Zmieniono {renamedCount} obiektów w {groupCount} grupach duplikatów.");
    }
}

#endif