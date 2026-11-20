#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class InteractionEventUsageScanner : EditorWindow
{
    // Wyszukujemy cztery metody InteractionEventLogger oraz bezposrednie LogEvent.
    // To analiza tekstowa: nie rozpoznaje typu obiektu po lewej stronie kropki.
    private static readonly Regex CallPattern = new Regex(
        @"(?:\?\.|\.)\s*(BeginObjectManipulation|EndObjectManipulation|LogPointEvent|FlushActiveManipulations|LogEvent)\s*\(",
        RegexOptions.Compiled);

    private readonly List<Usage> usages = new List<Usage>();
    private readonly Dictionary<Type, HashSet<string>> sceneLocations =
        new Dictionary<Type, HashSet<string>>();
    private readonly Dictionary<Type, HashSet<string>> prefabLocations =
        new Dictionary<Type, HashSet<string>>();

    private Vector2 scroll;
    private int checkedScenes;
    private int failedScenes;

    private sealed class Usage
    {
        public string ClassName;
        public Type ScriptType;
        public string ScriptPath;
        public string Method;
        public int Line;
    }

    [MenuItem("Tools/Telemetry/Interaction Event Usages")]
    private static void OpenWindow()
    {
        GetWindow<InteractionEventUsageScanner>("Telemetry Usages");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("InteractionEventLogger — miejsca uzycia", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Szukane metody: BeginObjectManipulation, EndObjectManipulation, " +
            "LogPointEvent, FlushActiveManipulations oraz bezposrednie LogEvent. " +
            "Skan obejmuje skrypty, " +
            "sceny i prefaby w katalogu Assets (takze nieaktywne obiekty).",
            MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Skanuj projekt", GUILayout.Height(28)))
                ScanProject();

            using (new EditorGUI.DisabledScope(usages.Count == 0))
            {
                if (GUILayout.Button("Eksport CSV", GUILayout.Height(28)))
                    ExportCsv();
            }
        }

        EditorGUILayout.LabelField(
            string.Format("Wywolania: {0}  |  Sprawdzone sceny: {1}  |  Bledy scen: {2}",
                usages.Count, checkedScenes, failedScenes));

        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (Usage usage in usages)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(
                    usage.ClassName + "  ->  " + usage.Method + "()", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Plik: " + usage.ScriptPath + ":" + usage.Line,
                    EditorStyles.wordWrappedLabel);

                DrawLocations("Sceny / GameObjecty:", GetLocations(sceneLocations, usage.ScriptType),
                    "Nie znaleziono komponentu tej klasy w zapisanych scenach.");
                DrawLocations("Prefaby / GameObjecty:", GetLocations(prefabLocations, usage.ScriptType),
                    "Nie znaleziono komponentu tej klasy w prefabach.");

                if (GUILayout.Button("Otworz kod (linia " + usage.Line + ")", GUILayout.Width(190)))
                {
                    MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(usage.ScriptPath);
                    if (script != null)
                        AssetDatabase.OpenAsset(script, usage.Line);
                }
            }
        }
        EditorGUILayout.EndScrollView();
    }

    private static void DrawLocations(string heading, IEnumerable<string> locations, string emptyText)
    {
        EditorGUILayout.LabelField(heading, EditorStyles.miniBoldLabel);
        List<string> items = locations.ToList();
        if (items.Count == 0)
        {
            EditorGUILayout.LabelField(emptyText, EditorStyles.wordWrappedMiniLabel);
            return;
        }

        foreach (string item in items)
            EditorGUILayout.LabelField("  " + item, EditorStyles.wordWrappedMiniLabel);
    }

    private static IEnumerable<string> GetLocations(
        Dictionary<Type, HashSet<string>> map, Type type)
    {
        if (type != null && map.TryGetValue(type, out HashSet<string> places))
            return places.OrderBy(p => p);
        return Enumerable.Empty<string>();
    }

    private void ScanProject()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Telemetry", "Uruchom skan w Edit Mode, nie w Play Mode.", "OK");
            return;
        }

        usages.Clear();
        sceneLocations.Clear();
        prefabLocations.Clear();
        checkedScenes = 0;
        failedScenes = 0;

        try
        {
            ScanCode();
            HashSet<Type> types = new HashSet<Type>(
                usages.Where(u => u.ScriptType != null &&
                                  typeof(MonoBehaviour).IsAssignableFrom(u.ScriptType))
                    .Select(u => u.ScriptType));

            if (types.Count > 0)
            {
                ScanScenes(types);
                ScanPrefabs(types);
            }

            usages.Sort((a, b) =>
            {
                int compare = string.Compare(a.ScriptPath, b.ScriptPath, StringComparison.Ordinal);
                return compare != 0 ? compare : a.Line.CompareTo(b.Line);
            });
        }
        catch (Exception ex)
        {
            Debug.LogError("[Telemetry Scan] " + ex);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            Repaint();
        }
    }

    private void ScanCode()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        foreach (string guid in AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                continue;

            string fullPath = Path.Combine(projectRoot, path);
            if (!File.Exists(fullPath))
                continue;

            string source = File.ReadAllText(fullPath);
            string searchable = MaskCommentsAndStrings(source);
            MonoScript monoScript = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            Type type = monoScript != null ? monoScript.GetClass() : null;
            string className = type != null
                ? type.FullName
                : Path.GetFileNameWithoutExtension(path) + " [typ nieustalony]";

            int cursor = 0;
            int line = 1;
            foreach (Match match in CallPattern.Matches(searchable))
            {
                int methodIndex = match.Groups[1].Index;
                for (int i = cursor; i < methodIndex; i++)
                    if (searchable[i] == '\n')
                        line++;
                cursor = methodIndex;

                usages.Add(new Usage
                {
                    ClassName = className,
                    ScriptType = type,
                    ScriptPath = path,
                    Method = match.Groups[1].Value,
                    Line = line
                });
            }
        }
    }

    private void ScanScenes(HashSet<Type> types)
    {
        string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
        for (int i = 0; i < sceneGuids.Length; i++)
        {
            string scenePath = AssetDatabase.GUIDToAssetPath(sceneGuids[i]);
            if (!scenePath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                continue;

            EditorUtility.DisplayProgressBar("Telemetry: skan scen", scenePath,
                (i + 1f) / sceneGuids.Length);

            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            try
            {
                if (openedHere)
                    scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (MonoBehaviour component in root.GetComponentsInChildren<MonoBehaviour>(true))
                    {
                        if (component == null || !types.Contains(component.GetType()))
                            continue;

                        AddLocation(sceneLocations, component.GetType(),
                            scenePath + "  ->  " + GetHierarchyPath(component.transform));
                    }
                }
                checkedScenes++;
            }
            catch (Exception ex)
            {
                failedScenes++;
                Debug.LogWarning("[Telemetry Scan] Nie udalo sie przeskanowac: " +
                                 scenePath + "\n" + ex.Message);
            }
            finally
            {
                if (openedHere && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    private void ScanPrefabs(HashSet<Type> types)
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
        for (int i = 0; i < prefabGuids.Length; i++)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
            EditorUtility.DisplayProgressBar("Telemetry: skan prefabow", prefabPath,
                (i + 1f) / prefabGuids.Length);

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
                continue;

            foreach (MonoBehaviour component in prefab.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component == null || !types.Contains(component.GetType()))
                    continue;

                AddLocation(prefabLocations, component.GetType(),
                    prefabPath + "  ->  " + GetHierarchyPath(component.transform));
            }
        }
    }

    private static void AddLocation(Dictionary<Type, HashSet<string>> map,
        Type type, string location)
    {
        if (!map.TryGetValue(type, out HashSet<string> locations))
        {
            locations = new HashSet<string>();
            map.Add(type, locations);
        }
        locations.Add(location);
    }

    private static string GetHierarchyPath(Transform transform)
    {
        Stack<string> names = new Stack<string>();
        for (Transform current = transform; current != null; current = current.parent)
            names.Push(current.name);
        return string.Join("/", names.ToArray());
    }

    // Zachowuje pozycje znakow i nowych linii; usuwa komentarze i zwykle stringi.
    // Nie jest to pelny parser C# (np. skomplikowane interpolacje/raw strings).
    private static string MaskCommentsAndStrings(string source)
    {
        char[] chars = source.ToCharArray();
        int mode = 0; // 0 kod, 1 //, 2 /* */, 3 string, 4 verbatim string, 5 char
        for (int i = 0; i < chars.Length; i++)
        {
            char c = source[i];
            char next = i + 1 < source.Length ? source[i + 1] : '\0';

            if (mode == 0)
            {
                if (c == '/' && next == '/') { Mask(chars, i); Mask(chars, ++i); mode = 1; }
                else if (c == '/' && next == '*') { Mask(chars, i); Mask(chars, ++i); mode = 2; }
                else if (c == '@' && next == '"') { Mask(chars, i); Mask(chars, ++i); mode = 4; }
                else if (c == '"') { Mask(chars, i); mode = 3; }
                else if (c == '\'') { Mask(chars, i); mode = 5; }
                continue;
            }

            Mask(chars, i);
            if (mode == 1 && c == '\n') mode = 0;
            else if (mode == 2 && c == '*' && next == '/')
            {
                Mask(chars, ++i);
                mode = 0;
            }
            else if ((mode == 3 || mode == 5) && c == '\\' && next != '\0')
                Mask(chars, ++i);
            else if (mode == 3 && c == '"') mode = 0;
            else if (mode == 5 && c == '\'') mode = 0;
            else if (mode == 4 && c == '"')
            {
                if (next == '"') Mask(chars, ++i);
                else mode = 0;
            }
        }
        return new string(chars);
    }

    private static void Mask(char[] chars, int index)
    {
        if (chars[index] != '\n' && chars[index] != '\r') chars[index] = ' ';
    }

    private void ExportCsv()
    {
        string output = EditorUtility.SaveFilePanel("Zapisz raport telemetry", "",
            "interaction-event-usages.csv", "csv");
        if (string.IsNullOrEmpty(output))
            return;

        using (StreamWriter writer = new StreamWriter(output, false, new UTF8Encoding(true)))
        {
            writer.WriteLine("Klasa;Metoda;Plik;Linia;Sceny_i_obiekty;Prefaby_i_obiekty");
            foreach (Usage usage in usages)
            {
                string scenes = string.Join(" | ", GetLocations(sceneLocations, usage.ScriptType).ToArray());
                string prefabs = string.Join(" | ", GetLocations(prefabLocations, usage.ScriptType).ToArray());
                writer.WriteLine(string.Join(";", new[]
                {
                    Csv(usage.ClassName), Csv(usage.Method), Csv(usage.ScriptPath),
                    usage.Line.ToString(), Csv(scenes), Csv(prefabs)
                }));
            }
        }
        Debug.Log("[Telemetry Scan] Zapisano raport: " + output);
    }

    private static string Csv(string value)
    {
        return "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
    }
}
#endif
