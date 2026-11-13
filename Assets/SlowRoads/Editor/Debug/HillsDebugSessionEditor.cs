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
        serializedObject.Update();Field(serializedObject,"settings","场景配置");Field(serializedObject,"environmentVolume","环境 Volume");Field(serializedObject,"environmentResources","环境渲染资源");serializedObject.ApplyModifiedProperties();
        var session=(HillsDebugSession)target;
        if(session.settings==null){EditorGUILayout.HelpBox("请指定丘陵场景配置。",MessageType.Warning);return;}
        var data=new SerializedObject(session.settings);data.Update();
        Field(data,"roadType","道路类型","选择后点击重新生成道路。宽度、路缘采样、越野规则和材质同步切换。");
        var selected=(SlowRoads.Scene.Road.HillsRoadType)data.FindProperty("roadType").enumValueIndex;
        var width=new SlowRoads.Generation.Road.RoadMeshConfig((SlowRoads.Generation.Road.HillsLaneStyle)selected,1).HalfWidth;
        EditorGUILayout.LabelField("路面宽度",$"全宽 {width*2:0.##} 米 / 半宽 {width:0.##} 米");
        if(selected==SlowRoads.Scene.Road.HillsRoadType.OffRoad||selected==SlowRoads.Scene.Road.HillsRoadType.OffRoadWide)
            EditorGUILayout.HelpBox("越野按原版隐藏普通铺装路面，仅桥区显示路面。普通越野区通过近路地形表现。",MessageType.Info);
        if(Application.isPlaying&&session.Streaming?.Core!=null&&(int)session.Streaming.Core.Config.Mesh.LaneStyle!=(int)selected)
            EditorGUILayout.HelpBox("道路类型已更改，点击下方“应用道路设置并重新生成”生效。",MessageType.Info);
        using(new EditorGUI.DisabledScope(Application.isPlaying))
        {
            Field(data,"seed","地图种子");
        }
        Field(data,"detail","道路视距档位","0–5，应用图形设置后生效。");
        Field(data,"terrainDetail","地形采样细度","0–4，与道路视距独立；应用图形设置后生效。");
        Field(data,"treeDensity","树密度（0自动）","按原版密度枚举生成；应用图形设置后生效。");
        Field(data,"terrainVisible","显示丘陵地形");Field(data,"terrainDebug","地形边界调试","远景绿/青、近路黄/橙表示细/粗，红色待显示，灰色驻留退役；浅绿框为近路内圈。");
        Field(data,"waterVisible","显示湖泊水面","仅控制显示，保留原版水面数据与其他内容随机序列。");
        Field(data,"roadsideVisible","显示路边标识与路桩");Field(data,"catsEyesVisible","显示道路反光道钉");Field(data,"observerReflectorsEnabled","观察者反光照明");
        Field(data,"treesVisible","显示树群","只控制显示，不重抽候选或改变随机序列。");Field(data,"treeShadows","树群投影","需要当前光源启用阴影；原版地表树影数据独立保留。");
        Field(data,"grassVisible","显示草与灌木","只控制显示，不重抽候选或改变随机序列。");
        Field(data,"fieldWallsVisible","显示农田边界墙");Field(data,"bridgesVisible","显示桥梁");Field(data,"wallsVisible","显示沿路墙及防护设施");
        Field(data,"season","地表季节（含主题光照）");Field(data,"environmentTime","主题光照时段");Field(data,"environmentWeather","主题光照天气");
        Field(data,"environmentLightingEnabled","应用主题光照");
        bool reapplyTheme=false;
        using(new EditorGUI.DisabledScope(!Application.isPlaying||session.Environment?.State==null))
            reapplyTheme=GUILayout.Button("重新应用地表主题");
        EditorGUILayout.HelpBox("地表24组主题直接应用到运行材质和太阳。天空、云、雾、雪由Hills Environment Volume上的WeatherController独立控制。",MessageType.Info);
        Field(data,"visible","显示道路");Field(data,"generationEnabled","持续生成","关闭时停止补路，观察者等待可用道路。");
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
            Field(data,"grassUploadInstancesPerFrame","草灌每帧准备量","分组或上传实例的工作量；降低可分摊上传成本，完整区域准备好后显示。");
            Field(data,"originShiftDistance","显示原点迁移距离（米）","降低远距离浮点抖动。");
            using(new EditorGUI.DisabledScope(Application.isPlaying))Field(data,"retainedBehindNodes","后方索引保留粗节点数","旧空间索引回收范围，重新生成生效。");
        }
        resources=EditorGUILayout.Foldout(resources,"材质资源",true);
        if(resources)
        {
            EditorGUILayout.PropertyField(data.FindProperty("cubeMaterial"));
            Field(data,"terrainMaterials","地形季节材质（夏/春/秋/冬）");
            Field(data,"treeMeshes","树模型（两族各四变体）");Field(data,"treeMaterials","树材质（夏/春/秋/冬各八套）");
            Field(data,"treeImposterMaterials","远景树材质（四季各两族）");Field(data,"treeCulling","树群GPU剔除");Field(data,"treeFallbackShader","树群降级Shader");
            Field(data,"grassMesh","草模型");Field(data,"bushMesh","灌木模型");
            Field(data,"grassMaterials","草季节材质（夏/春/秋/冬）");Field(data,"bushMaterials","灌木季节材质（夏/春/秋/冬）");
            Field(data,"signMeshes","标识模型（原十变体）");Field(data,"signMaterial","标识材质");Field(data,"vergeMarkerModel","路桩参考模型");Field(data,"vergeMarkerMaterials","路桩材质（黑/白/反光）");Field(data,"catsEyeMaterial","反光道钉材质");
            Field(data,"structureMaterials","桥墙季节材质（夏/春/秋/冬）");Field(data,"bridgeProjectedShadow","原版桥下投影材质");
            if(selected==SlowRoads.Scene.Road.HillsRoadType.Standard)
                foreach(var name in new[]{"summerMaterial","springMaterial","autumnMaterial","winterMaterial"})
                    EditorGUILayout.PropertyField(data.FindProperty(name));
            else EditorGUILayout.PropertyField(data.FindProperty(
                selected==SlowRoads.Scene.Road.HillsRoadType.OffRoad||selected==SlowRoads.Scene.Road.HillsRoadType.Single?"singleMaterials":"wideMaterials"),true);
        }
        data.ApplyModifiedProperties();
        if(reapplyTheme)session.Environment.ApplyThemeNow();
        if(!Application.isPlaying)EditorGUILayout.HelpBox("点击 Unity 顶部播放按钮后，可暂停、单步或重新生成。",MessageType.Info);
        using(new EditorGUI.DisabledScope(!Application.isPlaying||session.Observer==null))
        {
            if(GUILayout.Button(session.Observer!=null&&session.Observer.paused?"继续前进":"暂停前进"))session.TogglePause();
            if(GUILayout.Button("向前单步"))session.Step();
            if(GUILayout.Button("应用场景设置并重新生成"))session.Rebuild();
            using(new EditorGUI.DisabledScope(session.Streaming==null||session.Streaming.Core==null||session.settings.seed!=session.Streaming.Core.TerrainConfig.Seed||session.settings.LaneStyle!=session.Streaming.Core.Config.Mesh.LaneStyle))
                if(GUILayout.Button("应用图形设置（保留里程）"))session.RebuildGraphics();
        }
        if(session.Observer!=null)EditorGUILayout.LabelField("已行进距离",session.Observer.DistanceMetres.ToString("F2")+" 米");
        if(session.Streaming?.Core!=null)
        {
            EditorGUILayout.LabelField("道路状态",session.Streaming.Fault!=null?"生成失败":session.Streaming.Core.Ready?"可通行":"正在生成起始道路，请稍候");
            if(session.Streaming.GraphicsRebuildPending)EditorGUILayout.LabelField("图形重建","等待当前作业提交");
            var terrain=session.Streaming.Terrain?.Core;
            if(terrain!=null)EditorGUILayout.LabelField("地形状态",$"驻留 Tile {terrain.ActiveTiles} / Chunk {terrain.ActiveChunks} / 回收 {terrain.RetiredCount} / {(terrain.Building?"生成中":"等待下一需求")}");
            var trees=session.Streaming.Trees;
            if(trees?.Core!=null&&trees.Gpu!=null)EditorGUILayout.LabelField("树群状态",$"候选 {trees.Core.Store.Count} / 有效实例 {trees.Gpu.InstanceCount} / 批次 {trees.Gpu.BatchCount}");
            var grass=session.Streaming.Grass;
            if(grass?.Core!=null&&grass.Gpu!=null)EditorGUILayout.LabelField("草灌状态",$"完成区域 {grass.Core.CompletedCount} / 实例 {grass.Gpu.InstanceCount} / 批次 {grass.Gpu.BatchCount} / {(grass.Gpu.HasPendingUploads?"准备显示中":"显示已同步")}");
            var structures=session.Streaming.Structures;
            var roadside=session.Streaming.Roadside;
            if(roadside?.Core!=null)EditorGUILayout.LabelField("路边物件",$"对象 {roadside.ObjectCount} / 道钉 Mesh {roadside.EyeMeshCount} / 待安放 {roadside.Core.PendingCount}");
            if(structures!=null)EditorGUILayout.LabelField("农田与桥墙",$"农田 {session.Streaming.Terrain.Fields.ResidentRegions} / 桥 {structures.Bridges.Count} / 墙 {structures.Walls.Count} / Mesh {structures.NativeMeshCount}");
            if(session.Streaming.Fault!=null)EditorGUILayout.HelpBox(session.Streaming.Fault,MessageType.Error);
        }
        if(Application.isPlaying)Repaint();
    }
}
