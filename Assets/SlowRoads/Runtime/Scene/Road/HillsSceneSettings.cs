using System;
using UnityEngine;
using SlowRoads.Generation.Road;

namespace SlowRoads.Scene.Road
{
    public enum HillsRoadSeason { [InspectorName("夏季")] Summer, [InspectorName("春季")] Spring, [InspectorName("秋季")] Autumn, [InspectorName("冬季")] Winter }
    // Values preserve the original Wm enum. Default explicitly remains Standard for existing assets.
    public enum HillsRoadType { [InspectorName("宽越野")] OffRoadWide=0,[InspectorName("越野")] OffRoad=1,
        [InspectorName("单线窄路")] Single=2,[InspectorName("单线宽路")] SingleWide=3,[InspectorName("标准公路")] Standard=4 }
    [Serializable] public sealed class HillsSeasonMaterials
    {
        [InspectorName("夏季")] public Material summer;
        [InspectorName("春季")] public Material spring;
        [InspectorName("秋季")] public Material autumn;
        [InspectorName("冬季")] public Material winter;
        public Material Resolve(HillsRoadSeason season)=>season switch{
            HillsRoadSeason.Summer=>summer,HillsRoadSeason.Spring=>spring,HillsRoadSeason.Autumn=>autumn,
            HillsRoadSeason.Winter=>winter,_=>throw new ArgumentOutOfRangeException(nameof(season))};
    }

    // The only scene configuration. Pure generation snapshots seed/detail at construction.
    [CreateAssetMenu(menuName="Slow Roads/丘陵场景配置")]
    public sealed class HillsSceneSettings : ScriptableObject
    {
        [InspectorName("地图种子")] public string seed="a";
        [InspectorName("道路类型")] public HillsRoadType roadType=HillsRoadType.Standard;
        [InspectorName("道路视距档位"),Range(0,5)] public int detail=2;
        [InspectorName("地形采样细度"),Range(0,4)] public int terrainDetail=2;
        [InspectorName("季节")] public HillsRoadSeason season;
        [InspectorName("显示丘陵地形")] public bool terrainVisible=true;
        [InspectorName("地形边界调试")] public bool terrainDebug=false;
        [InspectorName("地形季节材质")] public Material[] terrainMaterials=new Material[4];
        public Material ResolveTerrainMaterial()=>terrainMaterials!=null&&terrainMaterials.Length==4&&terrainMaterials[(int)season]!=null?terrainMaterials[(int)season]:throw new InvalidOperationException("地形季节材质未配置");
        [InspectorName("显示道路")] public bool visible=true;
        [InspectorName("持续生成")] public bool generationEnabled=true;
        [InspectorName("自动前进")] public bool movementEnabled=true;
        [InspectorName("前进速度（米/秒）"),Min(0)] public float speed=8;
        [InspectorName("单步距离（米）"),Min(.01f)] public float stepMetres=1;
        [InspectorName("相机跟随")] public bool cameraFollowEnabled=true;
        [InspectorName("相机后退距离"),Min(0)] public float cameraDistance=14;
        [InspectorName("相机高度"),Min(.1f)] public float cameraHeight=8;
        [InspectorName("前视距离"),Min(0)] public float lookAhead=8;
        [InspectorName("跟随阻尼"),Min(0)] public float cameraDamping=6;
        // Performance controls are consumed by the live scheduler; these are not test presets.
        [InspectorName("每帧最大工作量"),Tooltip("算法步骤上限；降低会减慢预热和补路。"),Min(1)] public int operationsPerFrame=512;
        [InspectorName("CPU软预算（毫秒）"),Tooltip("工作块间让出；0仅使用工作量限制。"),Min(0)] public float cpuBudgetMilliseconds=2;
        [InspectorName("显示原点迁移距离（米）"),Min(100)] public float originShiftDistance=2048;
        [InspectorName("后方索引保留粗节点数"),Min(2)] public int retainedBehindNodes=100;
        [InspectorName("观察方块材质")] public Material cubeMaterial;
        [InspectorName("夏季路面材质")] public Material summerMaterial;
        [InspectorName("春季路面材质")] public Material springMaterial;
        [InspectorName("秋季路面材质")] public Material autumnMaterial;
        [InspectorName("冬季路面材质")] public Material winterMaterial;
        [InspectorName("单线窄路材质")] public HillsSeasonMaterials singleMaterials=new();
        [InspectorName("单线宽路材质")] public HillsSeasonMaterials wideMaterials=new();
        public HillsLaneStyle LaneStyle=>roadType switch {
            HillsRoadType.OffRoadWide=>HillsLaneStyle.OffRoadWide,HillsRoadType.OffRoad=>HillsLaneStyle.OffRoad,
            HillsRoadType.Single=>HillsLaneStyle.Single,HillsRoadType.SingleWide=>HillsLaneStyle.SingleWide,
            HillsRoadType.Standard=>HillsLaneStyle.Standard,_=>throw new ArgumentOutOfRangeException(nameof(roadType))};
        public double HalfWidth=>new RoadMeshConfig(LaneStyle,1).HalfWidth;
        public Material ResolveRoadMaterial(HillsLaneStyle laneStyle)
        {
            var material=laneStyle switch {
                HillsLaneStyle.OffRoad or HillsLaneStyle.Single=>singleMaterials.Resolve(season),
                HillsLaneStyle.OffRoadWide or HillsLaneStyle.SingleWide=>wideMaterials.Resolve(season),
                HillsLaneStyle.Standard=>season switch { HillsRoadSeason.Summer=>summerMaterial,HillsRoadSeason.Spring=>springMaterial,
                HillsRoadSeason.Autumn=>autumnMaterial,HillsRoadSeason.Winter=>winterMaterial,_=>throw new ArgumentOutOfRangeException(nameof(season)) },
                _=>throw new ArgumentOutOfRangeException(nameof(laneStyle))};
            if(material==null||material.shader==null||!material.shader.isSupported)throw new InvalidOperationException("当前季节的道路材质未配置或不受支持");
            return material;
        }
    }
}
