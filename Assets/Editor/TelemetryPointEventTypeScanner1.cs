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

/// <summary>
/// Edytorowy raport nazw eventType przekazywanych do LogPointEvent().
/// Skan tekstowy (nie analizator semantyczny C#): działa na wywołaniach
/// wielowierszowych, pomija komentarze i potrafi odczytać proste const string
/// zadeklarowane w tym samym pliku. Nie zgaduje wartości dynamicznych.
/// </summary>
public class TelemetryPointEventTypeScanner : EditorWindow
{
    private static readonly Regex CallPattern = new Regex(
        @"(?:\?\.\s*|\.\s*)LogPointEvent\s*\(", RegexOptions.Compiled);

    private static readonly Regex NormalStringPattern = new Regex(
        "^\\s*\"(?<text>(?:\\\\.|[^\"\\\\])*)\"\\s*$", RegexOptions.Compiled);

    private static readonly Regex VerbatimStringPattern = new Regex(
        "^\\s*@\"(?<text>(?:\"\"|[^\"])*)\"\\s*$", RegexOptions.Compiled);

    private static readonly Regex ConstDefinitionPattern = new Regex(
        @"\bconst\s+string\s+(?<name>[A-Za-z_]\w*)\s*=\s*(?<value>[^;]+);",
        RegexOptions.Compiled);

    private static readonly Regex SimpleIdentifierPattern = new Regex(
        @"^[A-Za-z_]\w*$", RegexOptions.Compiled);

    private static readonly HashSet<string> ExcludedTypes = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase) { "GAZE_FIXATION", "ROOM_TRANSITION" };

    private readonly List<Usage> usages = new List<Usage>();
    private readonly Dictionary<Type, HashSet<string>> sceneLocations =
        new Dictionary<Type, HashSet<string>>();
    private readonly Dictionary<Type, HashSet<string>> prefabLocations =
        new Dictionary<Type, HashSet<string>>();
    private readonly Dictionary<string, bool> expanded =
        new Dictionary<string, bool>(StringComparer.Ordinal);

    private Vector2 scroll;
    private int checkedScenes;
    private int failedScenes;
    private int excludedCalls;

    private sealed class Usage
    {
        public string EventType;       // null = nie udało się statycznie odczytać
        public string EventExpression; // oryginalny pierwszy argument
        public string Target;          // drugi argument, literał lub wyrażenie
        public string ExtraExpression; // trzeci argument (dla kontroli)
        public string ClassName;
        public Type ScriptType;
        public string ScriptPath;
        public int Line;
    }

    [MenuItem("Tools/Telemetry/Point Event Types")]
    private static void OpenWindow()
    {
        GetWindow<TelemetryPointEventTypeScanner>("Point Event Types");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Typy zdarzeń LogPointEvent()", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Zbiera pierwszy argument wywołań .LogPointEvent(...), np. POTION_CREATED. " +
            "Pomija GAZE_FIXATION i ROOM_TRANSITION. Pokazuje też klasy, sceny, " +
            "prefaby i lokalizacje w kodzie. Nieznane wyrażenia wskazuje osobno. " +
            "OBJECT_MANIPULATION jest logowane inną metodą i nie należy do tego raportu.",
            MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Skanuj projekt", GUILayout.Height(28)))
                ScanProject();

            using (new EditorGUI.DisabledScope(usages.Count == 0))
            {
                if (GUILayout.Button("Eksport: lista typów CSV", GUILayout.Height(28)))
                    ExportSummaryCsv();
                if (GUILayout.Button("Eksport: wywołania CSV", GUILayout.Height(28)))
                    ExportUsagesCsv();
            }
        }

        int distinct = usages.Where(u => u.EventType != null)
            .Select(u => u.EventType).Distinct(StringComparer.Ordinal).Count();
        int unknown = usages.Count(u => u.EventType == null);
        EditorGUILayout.LabelField(
            "Typy: " + distinct + " | Wywołania: " + usages.Count +
            " | Nierozpoznane: " + unknown + " | Pominięte: " + excludedCalls +
            " | Sceny: " + checkedScenes + " | Błędy: " + failedScenes);

        scroll = EditorGUILayout.BeginScrollView(scroll);

        foreach (IGrouping<string, Usage> group in usages
            .Where(u => u.EventType != null)
            .GroupBy(u => u.EventType)
            .OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            string eventName = group.Key;
            bool isExpanded = expanded.ContainsKey(eventName) && expanded[eventName];
            isExpanded = EditorGUILayout.Foldout(
                isExpanded, eventName + " (wywołań w kodzie: " + group.Count() + ")", true);
            expanded[eventName] = isExpanded;

            if (!isExpanded)
                continue;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Sceny: " + JoinSceneNames(group),
                    EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField("Klasy: " + string.Join(" | ",
                    group.Select(u => u.ClassName).Distinct().OrderBy(x => x).ToArray()),
                    EditorStyles.wordWrappedLabel);
                foreach (Usage usage in group)
                    DrawUsage(usage);
            }
        }

        List<Usage> unresolved = usages.Where(u => u.EventType == null).ToList();
        if (unresolved.Count > 0)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Wywołania z nierozpoznanym typem zdarzenia",
                EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Pierwszy argument jest wyrażeniem, zmienną albo stałą z innego pliku. " +
                "Otwórz wskazany kod, aby sprawdzić faktyczną nazwę eventType.",
                MessageType.Warning);
            foreach (Usage usage in unresolved)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    DrawUsage(usage);
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawUsage(Usage usage)
    {
        EditorGUILayout.LabelField(usage.ClassName + " — " + usage.ScriptPath + ":" + usage.Line,
            EditorStyles.wordWrappedLabel);
        EditorGUILayout.LabelField("eventType: " +
            (usage.EventType ?? "NIEUSTALONY: " + usage.EventExpression),
            EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.LabelField("targetObject: " + usage.Target,
            EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.LabelField("Sceny: " + JoinSceneNames(new[] { usage }),
            EditorStyles.wordWrappedMiniLabel);

        if (GUILayout.Button("Otwórz kod", GUILayout.Width(120)))
        {
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(usage.ScriptPath);
            if (script != null)
                AssetDatabase.OpenAsset(script, usage.Line);
        }
        EditorGUILayout.Space();
    }

    private void ScanProject()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Telemetry", "Skanuj w Edit Mode.", "OK");
            return;
        }

        usages.Clear();
        sceneLocations.Clear();
        prefabLocations.Clear();
        expanded.Clear();
        checkedScenes = 0;
        failedScenes = 0;
        excludedCalls = 0;

        try
        {
            ScanCode();
            HashSet<Type> componentTypes = new HashSet<Type>(usages
                .Where(u => u.ScriptType != null &&
                    typeof(MonoBehaviour).IsAssignableFrom(u.ScriptType))
                .Select(u => u.ScriptType));

            if (componentTypes.Count > 0)
            {
                ScanScenes(componentTypes);
                ScanPrefabs(componentTypes);
            }

            usages.Sort((a, b) =>
            {
                int result = string.CompareOrdinal(a.EventType ?? "\uffff", b.EventType ?? "\uffff");
                if (result != 0) return result;
                result = string.CompareOrdinal(a.ScriptPath, b.ScriptPath);
                return result != 0 ? result : a.Line.CompareTo(b.Line);
            });
            Debug.Log("[Point Event Scanner] Typy: " +
                usages.Where(u => u.EventType != null).Select(u => u.EventType).Distinct().Count() +
                ", wywołania: " + usages.Count + ", pominięte: " + excludedCalls);
        }
        catch (Exception ex)
        {
            Debug.LogError("[Point Event Scanner] " + ex);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            Repaint();
        }
    }

    private void ScanCode()
    {
        string root = Directory.GetParent(Application.dataPath).FullName;
        string[] scriptGuids = AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets" });

        for (int index = 0; index < scriptGuids.Length; index++)
        {
            string path = AssetDatabase.GUIDToAssetPath(scriptGuids[index]);
            if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                continue;

            string absolutePath = Path.Combine(root, path);
            if (!File.Exists(absolutePath))
                continue;

            string source = File.ReadAllText(absolutePath);
            string masked = MaskCommentsAndStrings(source);
            Dictionary<string, string> constants = GetStringConstants(source);
            MonoScript mono = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            Type type = mono != null ? mono.GetClass() : null;
            string className = type != null ? type.FullName :
                Path.GetFileNameWithoutExtension(path) + " [typ nieustalony]";

            foreach (Match match in CallPattern.Matches(masked))
            {
                int openParen = match.Index + match.Length - 1;
                List<string> args;
                if (!TryReadArguments(source, masked, openParen, out args) || args.Count < 2)
                    continue;

                string expression = args[0].Trim();
                string eventType;
                bool resolved = TryResolveType(expression, constants, out eventType);
                if (resolved && ExcludedTypes.Contains(eventType))
                {
                    excludedCalls++;
                    continue;
                }

                string targetLiteral;
                string target = TryParseStringLiteral(args[1], out targetLiteral)
                    ? targetLiteral : args[1].Trim();

                usages.Add(new Usage
                {
                    EventType = resolved ? eventType : null,
                    EventExpression = expression,
                    Target = target,
                    ExtraExpression = args.Count >= 3 ? args[2].Trim() : "",
                    ClassName = className,
                    ScriptType = type,
                    ScriptPath = path,
                    Line = 1 + CountNewlines(source, match.Index)
                });
            }
        }
    }

    private static int CountNewlines(string value, int untilIndex)
    {
        int count = 0;
        for (int i = 0; i < untilIndex; i++)
            if (value[i] == '\n') count++;
        return count;
    }

    // masked: komentarze i literały są zastąpione spacjami, więc przecinki
    // w stringach i komentarzach nie rozcinają listy argumentów.
    private static bool TryReadArguments(string original, string masked,
        int openParenIndex, out List<string> args)
    {
        args = new List<string>();
        int parens = 1;
        int brackets = 0;
        int braces = 0;
        int start = openParenIndex + 1;

        for (int i = start; i < masked.Length; i++)
        {
            switch (masked[i])
            {
                case '(':
                    parens++;
                    break;
                case ')':
                    parens--;
                    if (parens == 0)
                    {
                        args.Add(original.Substring(start, i - start));
                        return true;
                    }
                    break;
                case '[':
                    brackets++;
                    break;
                case ']':
                    brackets--;
                    break;
                case '{':
                    braces++;
                    break;
                case '}':
                    braces--;
                    break;
                case ',':
                    if (parens == 1 && brackets == 0 && braces == 0)
                    {
                        args.Add(original.Substring(start, i - start));
                        start = i + 1;
                    }
                    break;
            }
        }
        return false;
    }

    private static bool TryResolveType(string expression,
        Dictionary<string, string> constants, out string eventType)
    {
        if (TryParseStringLiteral(expression, out eventType))
            return true;

        string name = expression.Trim();
        if (SimpleIdentifierPattern.IsMatch(name) && constants.TryGetValue(name, out eventType))
            return true;

        eventType = null;
        return false;
    }

    private static bool TryParseStringLiteral(string expression, out string value)
    {
        Match match = VerbatimStringPattern.Match(expression.Trim());
        if (match.Success)
        {
            value = match.Groups["text"].Value.Replace("\"\"", "\"");
            return true;
        }

        match = NormalStringPattern.Match(expression.Trim());
        if (match.Success)
        {
            try
            {
                value = Regex.Unescape(match.Groups["text"].Value);
                return true;
            }
            catch (ArgumentException)
            {
                // Nieprawidłowy escape: nie przypisujemy nazwy na siłę.
            }
        }

        value = null;
        return false;
    }

    private static Dictionary<string, string> GetStringConstants(string source)
    {
        string withoutComments = MaskCommentsOnly(source);
        Dictionary<string, string> results = new Dictionary<string, string>(StringComparer.Ordinal);
        HashSet<string> duplicates = new HashSet<string>(StringComparer.Ordinal);

        foreach (Match match in ConstDefinitionPattern.Matches(withoutComments))
        {
            string name = match.Groups["name"].Value;
            string value;
            if (!TryParseStringLiteral(match.Groups["value"].Value, out value))
                continue;

            if (duplicates.Contains(name))
                continue;

            if (results.ContainsKey(name))
            {
                // Jeśli w pliku są różne klasy z tą samą nazwą stałej,
                // nie przypisujemy im błędnej wartości.
                results.Remove(name);
                duplicates.Add(name);
            }
            else
            {
                results.Add(name, value);
            }
        }
        return results;
    }

    // Dwa tryby maskowania z zachowaniem długości tekstu i pozycji linii.
    private static string MaskCommentsOnly(string source)
    {
        return MaskLexically(source, false);
    }

    private static string MaskCommentsAndStrings(string source)
    {
        return MaskLexically(source, true);
    }

    private static string MaskLexically(string source, bool maskLiterals)
    {
        char[] result = source.ToCharArray();
        // 0 kod, 1 komentarz //, 2 komentarz /* */, 3 string,
        // 4 string @"", 5 znak (char literal).
        int mode = 0;

        for (int i = 0; i < source.Length; i++)
        {
            char c = source[i];
            char next = i + 1 < source.Length ? source[i + 1] : '\0';

            if (mode == 0)
            {
                if (c == '/' && next == '/')
                {
                    Hide(result, i++);
                    Hide(result, i);
                    mode = 1;
                }
                else if (c == '/' && next == '*')
                {
                    Hide(result, i++);
                    Hide(result, i);
                    mode = 2;
                }
                else if (c == '@' && next == '"')
                {
                    if (maskLiterals) { Hide(result, i); Hide(result, i + 1); }
                    i++;
                    mode = 4;
                }
                else if (c == '"')
                {
                    if (maskLiterals) Hide(result, i);
                    mode = 3;
                }
                else if (c == '\'')
                {
                    if (maskLiterals) Hide(result, i);
                    mode = 5;
                }
                continue;
            }

            if (mode == 1)
            {
                Hide(result, i);
                if (c == '\n') mode = 0;
                continue;
            }

            if (mode == 2)
            {
                Hide(result, i);
                if (c == '*' && next == '/')
                {
                    Hide(result, ++i);
                    mode = 0;
                }
                continue;
            }

            if (maskLiterals) Hide(result, i);
            if ((mode == 3 || mode == 5) && c == '\\' && i + 1 < source.Length)
            {
                i++;
                if (maskLiterals) Hide(result, i);
            }
            else if (mode == 3 && c == '"') mode = 0;
            else if (mode == 5 && c == '\'') mode = 0;
            else if (mode == 4 && c == '"')
            {
                if (next == '"')
                {
                    i++;
                    if (maskLiterals) Hide(result, i);
                }
                else mode = 0;
            }
        }
        return new string(result);
    }

    private static void Hide(char[] chars, int index)
    {
        if (chars[index] != '\r' && chars[index] != '\n')
            chars[index] = ' ';
    }

    private void ScanScenes(HashSet<Type> types)
    {
        string[] guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (!path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                continue;

            EditorUtility.DisplayProgressBar("Telemetry: skan scen", path,
                (i + 1f) / guids.Length);

            Scene scene = SceneManager.GetSceneByPath(path);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            try
            {
                if (openedHere)
                    scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

                foreach (GameObject root in scene.GetRootGameObjects())
                foreach (MonoBehaviour component in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (component == null || !types.Contains(component.GetType()))
                        continue;
                    AddLocation(sceneLocations, component.GetType(),
                        path + "  ->  " + HierarchyPath(component.transform));
                }
                checkedScenes++;
            }
            catch (Exception ex)
            {
                failedScenes++;
                Debug.LogWarning("[Point Event Scanner] Błąd sceny " + path + ": " + ex.Message);
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
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            EditorUtility.DisplayProgressBar("Telemetry: skan prefabów", path,
                (i + 1f) / guids.Length);

            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root == null) continue;
            foreach (MonoBehaviour component in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component == null || !types.Contains(component.GetType()))
                    continue;
                AddLocation(prefabLocations, component.GetType(),
                    path + "  ->  " + HierarchyPath(component.transform));
            }
        }
    }

    private static void AddLocation(Dictionary<Type, HashSet<string>> locations,
        Type type, string location)
    {
        HashSet<string> entries;
        if (!locations.TryGetValue(type, out entries))
        {
            entries = new HashSet<string>(StringComparer.Ordinal);
            locations.Add(type, entries);
        }
        entries.Add(location);
    }

    private static string HierarchyPath(Transform t)
    {
        Stack<string> parts = new Stack<string>();
        for (Transform current = t; current != null; current = current.parent)
            parts.Push(current.name);
        return string.Join("/", parts.ToArray());
    }

    private IEnumerable<string> GetLocations(Dictionary<Type, HashSet<string>> map, Type type)
    {
        HashSet<string> items;
        return type != null && map.TryGetValue(type, out items)
            ? items.OrderBy(x => x, StringComparer.Ordinal)
            : Enumerable.Empty<string>();
    }

    private string JoinSceneNames(IEnumerable<Usage> selected)
    {
        string[] names = selected
            .SelectMany(u => GetLocations(sceneLocations, u.ScriptType))
            .Select(entry => entry.Split(new[] { "  ->  " }, StringSplitOptions.None)[0])
            .Select(Path.GetFileNameWithoutExtension)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();
        return names.Length > 0 ? string.Join(" | ", names) : "nieustalona (brak komponentu w zapisanych scenach)";
    }

    private void ExportSummaryCsv()
    {
        string path = EditorUtility.SaveFilePanel("Lista typów zdarzeń", "",
            "point-event-types.csv", "csv");
        if (string.IsNullOrEmpty(path)) return;

        using (StreamWriter writer = new StreamWriter(path, false, new UTF8Encoding(true)))
        {
            writer.WriteLine("Typ_zdarzenia;Liczba_wywolan_w_kodzie;Sceny;Klasy;Obiekty_docelowe;Pliki_i_linie");
            foreach (IGrouping<string, Usage> group in usages
                .Where(u => u.EventType != null).GroupBy(u => u.EventType)
                .OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                writer.WriteLine(string.Join(";", new[]
                {
                    Csv(group.Key),
                    group.Count().ToString(),
                    Csv(JoinSceneNames(group)),
                    Csv(JoinDistinct(group.Select(u => u.ClassName))),
                    Csv(JoinDistinct(group.Select(u => u.Target))),
                    Csv(JoinDistinct(group.Select(u => u.ScriptPath + ":" + u.Line)))
                }));
            }
        }
        Debug.Log("[Point Event Scanner] Lista typów zapisana: " + path);
    }

    private void ExportUsagesCsv()
    {
        string path = EditorUtility.SaveFilePanel("Wywołania LogPointEvent", "",
            "point-event-usages.csv", "csv");
        if (string.IsNullOrEmpty(path)) return;

        using (StreamWriter writer = new StreamWriter(path, false, new UTF8Encoding(true)))
        {
            writer.WriteLine("Typ_zdarzenia;Pierwszy_argument;TargetObject;ExtraJson_argument;Klasa;Plik;Linia;Sceny;Sceny_i_obiekty;Prefaby_i_obiekty");
            foreach (Usage u in usages)
            {
                writer.WriteLine(string.Join(";", new[]
                {
                    Csv(u.EventType ?? "[NIEUSTALONY]"),
                    Csv(u.EventExpression),
                    Csv(u.Target),
                    Csv(u.ExtraExpression),
                    Csv(u.ClassName),
                    Csv(u.ScriptPath),
                    u.Line.ToString(),
                    Csv(JoinSceneNames(new[] { u })),
                    Csv(string.Join(" | ", GetLocations(sceneLocations, u.ScriptType).ToArray())),
                    Csv(string.Join(" | ", GetLocations(prefabLocations, u.ScriptType).ToArray()))
                }));
            }
        }
        Debug.Log("[Point Event Scanner] Wywołania zapisane: " + path);
    }

    private static string JoinDistinct(IEnumerable<string> input)
    {
        return string.Join(" | ", input.Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(s => s, StringComparer.Ordinal).ToArray());
    }

    private static string Csv(string text)
    {
        return "\"" + (text ?? "").Replace("\"", "\"\"") + "\"";
    }
}
#endif
