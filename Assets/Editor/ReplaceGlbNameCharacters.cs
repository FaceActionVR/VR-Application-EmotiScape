#if UNITY_EDITOR

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ReplaceGlbNameCharacters
{
    [MenuItem("Tools/GLB/Zamieñ kropki i spacje na podkreœlenia")]
    private static void ReplaceCharactersInActiveScene()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning(
                "[GLB] WyjdŸ z Play Mode przed u¿yciem narzêdzia.");
            return;
        }

        Scene activeScene = SceneManager.GetActiveScene();

        if (!activeScene.IsValid() || !activeScene.isLoaded)
        {
            Debug.LogError(
                "[GLB] Aktywna scena jest nieprawid³owa lub nieza³adowana.");
            return;
        }

        GameObject[] rootObjects = activeScene.GetRootGameObjects();
        List<Transform> allSceneTransforms = new List<Transform>();

        foreach (GameObject root in rootObjects)
        {
            allSceneTransforms.AddRange(
                root.GetComponentsInChildren<Transform>(true));
        }

        int renamedCount = 0;

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName(
            "Zamieñ kropki i spacje w nazwach");

        int undoGroupIndex = Undo.GetCurrentGroup();

        foreach (Transform t in allSceneTransforms)
        {
            if (t == null)
                continue;

            // Ignoruj ukryte/systemowe obiekty Editora
            if ((t.gameObject.hideFlags & HideFlags.HideInHierarchy) != 0 ||
                (t.gameObject.hideFlags & HideFlags.NotEditable) != 0)
            {
                continue;
            }

            string oldName = t.name;

            string newName = oldName
                .Replace('.', '_')
                .Replace(' ', '_');

            if (oldName == newName)
                continue;

            Undo.RecordObject(
                t.gameObject,
                "GLB Replace Name Characters");

            t.name = newName;

            EditorUtility.SetDirty(t.gameObject);

            PrefabUtility.RecordPrefabInstancePropertyModifications(
                t.gameObject);

            Debug.Log(
                $"[GLB] '{oldName}' -> '{newName}'",
                t.gameObject);

            renamedCount++;
        }

        Undo.CollapseUndoOperations(undoGroupIndex);

        if (renamedCount > 0)
        {
            EditorSceneManager.MarkSceneDirty(activeScene);
        }

        Debug.Log(
            $"[GLB] Gotowe dla sceny '{activeScene.name}'. " +
            $"Zmieniono {renamedCount} nazw.");
    }
}

#endif