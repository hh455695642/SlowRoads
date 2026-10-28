using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using SlowRoads.Generation.Midline;
using SlowRoads.Generation.Road;
using SlowRoads.Scene.Road;
using UnityEditor;
using UnityEngine;

// Explicit CLI/Inspector authoring only. No startup hooks or directory-wide regeneration menus.
public static class HillsRoadMaterialAuthoring
{
    const string Textures="Assets/SlowRoads/Art/Reference/SlowRoads/Textures/Road/";
    const string Materials="Assets/SlowRoads/Art/Reference/SlowRoads/Materials/Road/";
    public const string SettingsPath="Assets/SlowRoads/Settings/Hills/HillsRoadMaterialSettings.asset";
    const string PreviewRoot="Assets/SlowRoads/Art/Debug/RoadPreview/";
    public const string PreviewPath=PreviewRoot+"HillsRoadFinitePreview.asset";
    static void Folder(string path)
    {
        var parts=path.TrimEnd('/').Split('/');string parent=parts[0];
        for(int i=1;i<parts.Length;i++){string next=parent+"/"+parts[i];if(!AssetDatabase.IsValidFolder(next))AssetDatabase.CreateFolder(parent,parts[i]);parent=next;}
    }
    static Texture2D Map(string name,int aniso)
    {
        string path=Textures+name+".png";var importer=AssetImporter.GetAtPath(path) as TextureImporter;
        if(importer==null)throw new FileNotFoundException(path);
        // Original Et loader defaults: sRGB RGB, untouched linear alpha, flipY=true, repeat+mips.
        importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;importer.alphaSource=TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency=false;importer.mipmapEnabled=true;importer.wrapMode=TextureWrapMode.Repeat;
        importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=aniso;importer.isReadable=false;
        importer.textureCompression=TextureImporterCompression.CompressedHQ;
        var desktop=importer.GetPlatformTextureSettings("Standalone");desktop.name="Standalone";desktop.overridden=true;
        desktop.format=TextureImporterFormat.BC7;desktop.compressionQuality=100;desktop.maxTextureSize=2048;importer.SetPlatformTextureSettings(desktop);
        foreach(string platform in new[]{"Android","iPhone"})
        {var p=importer.GetPlatformTextureSettings(platform);p.name=platform;p.overridden=true;p.format=TextureImporterFormat.ASTC_6x6;p.maxTextureSize=2048;importer.SetPlatformTextureSettings(p);}
        importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    public static string Provision()
    {
        var shader=Shader.Find("SlowRoads/Hills/Road");if(shader==null)throw new InvalidOperationException("Road Shader not imported");
        Folder(Materials);Folder("Assets/SlowRoads/Settings/Hills");
        var settings=AssetDatabase.LoadAssetAtPath<HillsRoadMaterialSettings>(SettingsPath);
        if(settings==null){settings=ScriptableObject.CreateInstance<HillsRoadMaterialSettings>();AssetDatabase.CreateAsset(settings,SettingsPath);}
        var entries=new List<HillsRoadMaterialEntry>();
        var cache=new Dictionary<string,Texture2D>();
        Texture2D Get(string name,int aniso){if(!cache.TryGetValue(name,out var map))cache[name]=map=Map(name,aniso);return map;}
        foreach(HillsRoadSeason season in Enum.GetValues(typeof(HillsRoadSeason)))foreach(HillsRoadSurface surface in Enum.GetValues(typeof(HillsRoadSurface)))
        {
            bool winter=season==HillsRoadSeason.Winter,paint=surface==HillsRoadSurface.Standard&&!winter;
            string baseName=surface==HillsRoadSurface.Standard?(winter?"winter_road":"road_base"):
                surface==HillsRoadSurface.Single?(winter?"winter_road_single":season==HillsRoadSeason.Spring?"road_single_base_spring":"road_single_base"):
                winter?"road_single_wide_winter":"road_single_wide";
            string file=Materials+surface+"_"+season+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(file);
            if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,file);}else material.shader=shader;
            material.enableInstancing=true;material.SetTexture("_BaseMap",Get(baseName,paint?4:16));
            material.SetColor("_BaseColor",winter?Color.white:new Color(221f/255,221f/255,221f/255,1).linear);
            material.SetFloat("_TextureStretch",surface==HillsRoadSurface.Single?2:1);material.SetFloat("_HasPaint",paint?1:0);
            string[] props={"_DashedMap","_SolidMap","_SolidLeftMap","_SolidRightMap","_MergeLeftMap","_MergeRightMap"};
            string[] files={"dashed","solid","solid_left","solid_right","merge_left","merge_right"};
            for(int j=0;j<props.Length;j++)material.SetTexture(props[j],paint?Get("road_paint_"+files[j],16):Texture2D.blackTexture);
            bool overlay=season==HillsRoadSeason.Spring||season==HillsRoadSeason.Autumn;
            material.SetFloat("_HasOverlay",overlay?1:0);
            material.SetTexture("_OverlayMap",overlay?Get((surface==HillsRoadSurface.Single?"road_single_overlay_":"road_overlay_")+season.ToString().ToLowerInvariant(),4):Texture2D.blackTexture);
            material.SetFloat("_ShadowFactor",1);material.SetFloat("_FresnelIntensity",1);material.SetFloat("_Radiance",0);
            material.SetFloat("_SurfaceOnly",0);material.SetFloat("_Cull",2);EditorUtility.SetDirty(material);
            entries.Add(new HillsRoadMaterialEntry{season=season,surface=surface,material=material});
        }
        settings.entries=entries.ToArray();settings.grayMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/SlowRoads/Art/Debug/Materials/HillsDebugRoad.mat");
        EditorUtility.SetDirty(settings);AssetDatabase.SaveAssets();return "12 shared materials; source maps imported; existing GUIDs preserved";
    }

    [Serializable] sealed class Matrix{public Case[] cases;}
    [Serializable] sealed class Case{public string id;public Config configuration;public Input input;public Build[] generations;}
    [Serializable] sealed class Config{public int intervals,laneStyle;public bool baseOnly;}
    [Serializable] sealed class Input{public Node[] coarse,points;}
    [Serializable] sealed class Node{public int index,owner,previous,next;public bool isCoarse,bridge,hasLeft,hasRight,hasMergeLeft,hasMergeRight,mergeLeft,mergeRight,hasShadowLeft,hasShadowRight;public string[] p,side;public string left,right,shadowLeft,shadowRight;}
    [Serializable] sealed class Build{public int startIndex;public Before before;}
    [Serializable] sealed class Before{public string phase;}
    static double D(string bits)=>BitConverter.Int64BitsToDouble(unchecked((long)ulong.Parse(bits,NumberStyles.HexNumber,CultureInfo.InvariantCulture)));
    static Double3 V(string[] b)=>new Double3(D(b[0]),D(b[1]),D(b[2]));
    public static string Bake(string caseId="natural-style3")
    {
        string fixture=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Docs/ReverseEngineering/RoadAudit/Fixtures/RoadMesh/hills-road-mesh.json");
        var c=JsonUtility.FromJson<Matrix>(File.ReadAllText(fixture)).cases.First(x=>x.id==caseId);
        var coarse=c.input.coarse.Select(n=>new RoadCoarseSample(n.index,V(n.p),n.bridge,D(n.left),D(n.right),n.hasLeft,n.hasRight,n.mergeLeft,n.mergeRight,n.hasMergeLeft,n.hasMergeRight)).ToArray();
        var fine=c.input.points.Select(n=>new RoadFineSample(n.owner,n.previous,n.next,n.isCoarse,V(n.p),V(n.side),D(n.shadowLeft),D(n.shadowRight),n.hasShadowLeft,n.hasShadowRight)).ToArray();
        var path=new RoadPathSnapshot(1,1,coarse,fine,int.MaxValue,int.MaxValue,int.MaxValue);
        var config=new RoadMeshConfig((HillsLaneStyle)c.configuration.laneStyle,c.configuration.intervals,c.configuration.baseOnly);
        var overlay=new RoadAttributeOverlay(path);var routes=new List<Vector3>();var origins=new List<Vector3>();var meshes=new List<Mesh>();
        Folder(PreviewRoot);var first=coarse.First(n=>n.Index==c.generations[0].startIndex).Position;
        double phase=D(c.generations[0].before.phase);
        for(int i=0;i<2;i++)
        {
            var builder=new RoadMeshBuilder(new RoadMeshBuffers(config));var step=c.generations[i];
            var request=new RoadSegmentRequest(i,1,1,step.startIndex,config.Id,phase);
            if(builder.Begin(request,path,overlay)!=RoadBuildResult.Building||builder.Advance(64)!=RoadBuildResult.Complete)throw new InvalidOperationException("P4 input is incomplete");
            phase=builder.State.Phase;using var upload=new RoadMeshUploader("P4 baked "+i);
            upload.Upload(builder.Buffers,1,1);var b=builder.Buffers;
            string output=PreviewRoot+"Segment"+i+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(output);
            if(mesh==null){mesh=UnityEngine.Object.Instantiate(upload.Mesh);AssetDatabase.CreateAsset(mesh,output);}else EditorUtility.CopySerialized(upload.Mesh,mesh);
            EditorUtility.SetDirty(mesh);meshes.Add(mesh);
            var o=new Vector3((float)(b.Origin.X-first.X),0,(float)(b.Origin.Z-first.Z));origins.Add(o);
            for(int j=0;j<b.VertexCount;j+=2){if(i>0&&j==0)continue;routes.Add(o+new Vector3((b.Position[j*3]+b.Position[(j+1)*3])*.5f,b.Position[j*3+1],(b.Position[j*3+2]+b.Position[(j+1)*3+2])*.5f));}
        }
        var preview=AssetDatabase.LoadAssetAtPath<HillsRoadFinitePreview>(PreviewPath);
        if(preview==null){preview=ScriptableObject.CreateInstance<HillsRoadFinitePreview>();AssetDatabase.CreateAsset(preview,PreviewPath);}
        preview.meshes=meshes.ToArray();preview.origins=origins.ToArray();preview.route=routes.ToArray();preview.materials=AssetDatabase.LoadAssetAtPath<HillsRoadMaterialSettings>(SettingsPath);preview.sourceCase=caseId;
        EditorUtility.SetDirty(preview);AssetDatabase.SaveAssets();return caseId+": two P4-generated asset meshes, "+routes.Count+" route samples";
    }
}
