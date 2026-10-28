using UnityEngine;
using UnityEditor;
using SlowRoads.Debugging;

[CustomEditor(typeof(HillsDebugSession))]
public sealed class HillsDebugSessionEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var session = (HillsDebugSession)target;
        EditorGUILayout.HelpBox(session.IsFinitePreview?"P4 finite road + material preview. Stops at the end; P5 streaming is pending.":"Legacy route adapter: approximate preview.",MessageType.Info);
        using(new EditorGUI.DisabledScope(Application.isPlaying))
        {
            if(GUILayout.Button("Bake two P4 road segments"))Debug.Log(HillsRoadMaterialAuthoring.Bake());
        }
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

