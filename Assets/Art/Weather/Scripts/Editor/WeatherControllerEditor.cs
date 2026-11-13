using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SlowRoads.Environment.Editor
{
    [InitializeOnLoad, CustomEditor(typeof(WeatherController))]
    public sealed class WeatherControllerEditor : UnityEditor.Editor
    {
        static WeatherControllerEditor()
        {
            EditorApplication.update+=RefreshPending;
            WeatherController.Applied+=MarkDirty;
            Undo.undoRedoPerformed+=()=>{foreach(var c in WeatherController.Editors)if(c!=null)c.AcceptCurrentParameters();SceneView.RepaintAll();};
            EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredEditMode || state==PlayModeStateChange.EnteredPlayMode)foreach(var c in WeatherController.Editors)if(c!=null)c.RequestRefresh();};
        }
        static void RefreshPending()
        {
            if(EditorApplication.isCompiling || EditorApplication.isUpdating)return;
            foreach(var c in WeatherController.Editors)if(c!=null && c.isActiveAndEnabled && c.NeedsRefresh)ApplyWithUndo(c);
        }
        public static void ApplyWithUndo(WeatherController c)
        {
            var v=c.Volume;var p=v.sharedProfile;if(p==null){c.ApplyWeather();return;}
            // One undo group owns only weather subassets; Lighting is never recorded or dirtied.
            Undo.RecordObject(p,"应用天气");
            Ensure<EnvironmentSkyVolume>(p);Ensure<EnvironmentCloudVolume>(p);Ensure<EnvironmentFogVolume>(p);Ensure<EnvironmentSnowVolume>(p);
            foreach(var x in p.components)if(WeatherProfileWriter.IsWeather(x))Undo.RecordObject(x,"应用天气");
            if(v.HasInstantiatedProfile())foreach(var x in v.profile.components)if(WeatherProfileWriter.IsWeather(x))Undo.RecordObject(x,"应用天气");
            c.ApplyWeather();
        }
        static void Ensure<T>(VolumeProfile p) where T:VolumeComponent
        {
            if(p.Has<T>())return;
            var c=p.Add<T>(true);c.name=typeof(T).Name;Undo.RegisterCreatedObjectUndo(c,"添加天气组件");
            if(AssetDatabase.Contains(p))AssetDatabase.AddObjectToAsset(c,p);
        }
        static void MarkDirty(WeatherController c)
        {
            var p=c.Volume.sharedProfile;if(p==null)return;
            foreach(var x in p.components)if(WeatherProfileWriter.IsWeather(x))EditorUtility.SetDirty(x);
            EditorUtility.SetDirty(p);SceneView.RepaintAll();EditorApplication.QueuePlayerLoopUpdate();
        }
        public override void OnInspectorGUI()
        {
            serializedObject.Update();EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("weather"),new GUIContent("天气设置"),true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("resources"),new GUIContent("天气渲染资源"));
            if(EditorGUI.EndChangeCheck()){serializedObject.ApplyModifiedProperties();ApplyWithUndo((WeatherController)target);}else serializedObject.ApplyModifiedProperties();
            var c=(WeatherController)target;
            EditorGUILayout.HelpBox("独立控制天空、云、雾和雪。地表季节与主题光照仍由HillsDebugSession控制。修改这里会覆盖Profile的四类天气参数。运行时为试调，退出后按编辑状态重新应用。",MessageType.Info);
            if(c.Error!=null)EditorGUILayout.HelpBox(c.Error,MessageType.Warning);
            if(GUILayout.Button("应用天气到Profile"))ApplyWithUndo(c);
            using(new EditorGUI.DisabledScope(c.Volume.sharedProfile==null || Application.isPlaying))
                if(GUILayout.Button("保存天气Profile"))AssetDatabase.SaveAssetIfDirty(c.Volume.sharedProfile);
        }
    }
}
