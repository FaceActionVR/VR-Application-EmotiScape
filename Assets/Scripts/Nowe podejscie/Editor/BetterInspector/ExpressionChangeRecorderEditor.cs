using UnityEditor;
using UnityEngine;
using static ExpressionChangeRecorder;

[CustomEditor(typeof(ExpressionChangeRecorder))]
public class ExpressionChangeRecorderEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var t = (ExpressionChangeRecorder)target;

        // Nag³ówek
        EditorGUILayout.LabelField("Sampling Configuration", EditorStyles.boldLabel);

        // Enum – tryb próbkowania
        t.samplingMode = (SamplingMode)EditorGUILayout.EnumPopup("Sampling Mode", t.samplingMode);

        // W zale¿noœci od trybu pokazujemy jedno z pól:
        if (t.samplingMode == SamplingMode.FPS)
        {
            t.samplesPerSecond = EditorGUILayout.FloatField("Samples Per Second", t.samplesPerSecond);
        }
        else
        {
            t.intervalSeconds = EditorGUILayout.FloatField("Interval (Seconds)", t.intervalSeconds);
        }

        EditorGUILayout.Space();
        DrawDefaultInspectorExceptSampling();

        if (GUI.changed)
        {
            EditorUtility.SetDirty(t);
        }
    }

    void DrawDefaultInspectorExceptSampling()
    {
        // Rysuje wszystkie pola oprócz naszych trzech
        serializedObject.Update();

        SerializedProperty prop = serializedObject.GetIterator();
        bool first = true;
        while (prop.NextVisible(first))
        {
            first = false;
            if (prop.name == "samplingMode" ||
                prop.name == "samplesPerSecond" ||
                prop.name == "intervalSeconds")
                continue;

            EditorGUILayout.PropertyField(prop, true);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
