using UnityEngine;
using UnityEditor;
using SlowRoads.Debugging;

[CustomEditor(typeof(HillsDebugSession))]
public sealed class HillsDebugSessionEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.HelpBox("Legacy route adapter: debug framework only; Hills algorithm parity is pending.", MessageType.Info);
        var session = (HillsDebugSession)target;
        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button("Pause / Resume")) session.TogglePause();
            if (GUILayout.Button("Step Forward (metres)")) session.Step();
            if (GUILayout.Button("Reset Fixed Seed")) session.Rebuild();
        }
        if (session.Observer != null)
            EditorGUILayout.LabelField("Distance", session.Observer.Distance.ToString("F2") + " m");
        if (session.Road != null)
            EditorGUILayout.LabelField("Retained Nodes", session.Road.NodeCount.ToString());
        if (Application.isPlaying) Repaint();
    }
}

