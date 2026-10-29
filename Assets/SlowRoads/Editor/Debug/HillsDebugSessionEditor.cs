using UnityEngine;
using UnityEditor;
using SlowRoads.Debugging;

[CustomEditor(typeof(HillsDebugSession))]
public sealed class HillsDebugSessionEditor : Editor
{
    bool cameraSettings,performance,resources;
    static void Field(SerializedObject data,string name,string label,string tip="")
        =>EditorGUILayout.PropertyField(data.FindProperty(name),new GUIContent(label,tip),true);
    public override void OnInspectorGUI()
    {
        serializedObject.Update();Field(serializedObject,"settings","场景配置");serializedObject.ApplyModifiedProperties();
        var session=(HillsDebugSession)target;
        if(session.settings==null){EditorGUILayout.HelpBox("请指定丘陵场景配置。",MessageType.Warning);return;}
        var data=new SerializedObject(session.settings);data.Update();
        Field(data,"roadType","道路类型","选择后点击重新生成道路。宽度、路缘采样、越野规则和材质同步切换。");
        var selected=(SlowRoads.Scene.Road.HillsRoadType)data.FindProperty("roadType").enumValueIndex;
        var width=new SlowRoads.Generation.Road.RoadMeshConfig((SlowRoads.Generation.Road.HillsLaneStyle)selected,1).HalfWidth;
        EditorGUILayout.LabelField("路面宽度",$"全宽 {width*2:0.##} 米 / 半宽 {width:0.##} 米");
        if(selected==SlowRoads.Scene.Road.HillsRoadType.OffRoad||selected==SlowRoads.Scene.Road.HillsRoadType.OffRoadWide)
            EditorGUILayout.HelpBox("越野按原版隐藏普通铺装路面，仅桥区显示路面。当前场景尚无地形，普通越野区会显示背景。",MessageType.Info);
        if(Application.isPlaying&&session.Streaming?.Core!=null&&(int)session.Streaming.Core.Config.Mesh.LaneStyle!=(int)selected)
            EditorGUILayout.HelpBox("道路类型已更改，点击下方“应用道路设置并重新生成”生效。",MessageType.Info);
        using(new EditorGUI.DisabledScope(Application.isPlaying))
        {
            Field(data,"seed","地图种子");Field(data,"detail","道路视距档位","0–5，影响前方生成范围；重新生成生效。");
        }
        Field(data,"season","季节");Field(data,"visible","显示道路");Field(data,"generationEnabled","持续生成","关闭时停止补路，观察者等待可用道路。");
        EditorGUILayout.Space();Field(data,"movementEnabled","自动前进");Field(data,"speed","前进速度（米/秒）");Field(data,"stepMetres","单步距离（米）");
        cameraSettings=EditorGUILayout.Foldout(cameraSettings,"相机",true);
        if(cameraSettings)
        {
            Field(data,"cameraFollowEnabled","跟随观察方块");Field(data,"cameraDistance","后退距离");Field(data,"cameraHeight","高度");
            Field(data,"lookAhead","前视距离");Field(data,"cameraDamping","跟随阻尼");
        }
        performance=EditorGUILayout.Foldout(performance,"性能与回收",true);
        if(performance)
        {
            Field(data,"operationsPerFrame","每帧最大工作量","算法步骤上限；降低会减慢预热和补路。");
            Field(data,"cpuBudgetMilliseconds","CPU软预算（毫秒）","工作块之间检查耗时；0仅使用工作量限制，不是硬截止。");
            Field(data,"originShiftDistance","显示原点迁移距离（米）","降低远距离浮点抖动。");
            using(new EditorGUI.DisabledScope(Application.isPlaying))Field(data,"retainedBehindNodes","后方索引保留粗节点数","旧空间索引回收范围，重新生成生效。");
        }
        resources=EditorGUILayout.Foldout(resources,"材质资源",true);
        if(resources)
        {
            EditorGUILayout.PropertyField(data.FindProperty("cubeMaterial"));
            if(selected==SlowRoads.Scene.Road.HillsRoadType.Standard)
                foreach(var name in new[]{"summerMaterial","springMaterial","autumnMaterial","winterMaterial"})
                    EditorGUILayout.PropertyField(data.FindProperty(name));
            else EditorGUILayout.PropertyField(data.FindProperty(
                selected==SlowRoads.Scene.Road.HillsRoadType.OffRoad||selected==SlowRoads.Scene.Road.HillsRoadType.Single?"singleMaterials":"wideMaterials"),true);
        }
        data.ApplyModifiedProperties();
        if(!Application.isPlaying)EditorGUILayout.HelpBox("点击 Unity 顶部播放按钮后，可暂停、单步或重新生成。",MessageType.Info);
        using(new EditorGUI.DisabledScope(!Application.isPlaying||session.Observer==null))
        {
            if(GUILayout.Button(session.Observer!=null&&session.Observer.paused?"继续前进":"暂停前进"))session.TogglePause();
            if(GUILayout.Button("向前单步"))session.Step();
            if(GUILayout.Button("应用道路设置并重新生成"))session.Rebuild();
        }
        if(session.Observer!=null)EditorGUILayout.LabelField("已行进距离",session.Observer.DistanceMetres.ToString("F2")+" 米");
        if(session.Streaming?.Core!=null)
        {
            EditorGUILayout.LabelField("道路状态",session.Streaming.Fault!=null?"生成失败":session.Streaming.Core.Ready?"可通行":"正在生成起始道路，请稍候");
            if(session.Streaming.Fault!=null)EditorGUILayout.HelpBox(session.Streaming.Fault,MessageType.Error);
        }
        if(Application.isPlaying)Repaint();
    }
}
