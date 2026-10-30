// T5 fixed-view regression, invoked explicitly through Unity CLI in the main scene.
// It freezes production inputs; native geometry/GLSL expected images are generated
// separately by road-blend-reference.cjs --terrain-visual. No scene/asset writes.
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using SlowRoads.Debugging;
using SlowRoads.Generation.Midline;
using SlowRoads.Generation.Terrain;
using SlowRoads.Scene.Terrain;

public static class T5TerrainDisplayCheck
{
    const BindingFlags Fields=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static string Bits(double v)=>unchecked((ulong)BitConverter.DoubleToInt64Bits(v)).ToString("x16");
    static string[] Numbers(params double[] v)=>v.Select(Bits).ToArray();
    static float[] Matrix(Matrix4x4 m)=>Enumerable.Range(0,16).Select(i=>m[i]).ToArray();
    static float[] Vec(Vector3 v)=>new[]{v.x,v.y,v.z};
    static object Read(object o,string name)=>o.GetType().GetField(name,Fields).GetValue(o);
    public static string Capture()
    {
        if(!EditorApplication.isPlaying)throw new InvalidOperationException("Use the running main scene");
        var session=UnityEngine.Object.FindFirstObjectByType<HillsDebugSession>();
        if(session==null||!session.Streaming.Core.Ready)throw new InvalidOperationException("Road not Ready");
        session.Observer.paused=true;EditorApplication.isPaused=true;
        var road=session.Streaming;var terrain=road.Terrain;var core=road.Core;var config=core.TerrainConfig;
        var label=(core.Midline.State.ObserverIndex>1000?"bridge":"initial")+(session.settings.terrainDetail==0?"-d0":"");
        var directory=Path.GetFullPath("Temp/TerrainVisual/"+label);Directory.CreateDirectory(directory);
        var origin=new Vector3((float)road.RenderOrigin.X,(float)road.RenderOrigin.Y,(float)road.RenderOrigin.Z);
        var height=typeof(SlowRoads.Generation.Streaming.RoadStreamingSession).GetField("terrainHeightSource",Fields).GetValue(core);
        int negative=(int)height.GetType().GetProperty("NegativeXLayer").GetValue(height);
        int positive=(int)height.GetType().GetProperty("NonNegativeXLayer").GetValue(height);
        var parts=new List<object>();var draws=new List<(Mesh mesh,Matrix4x4 matrix)>();
        var active=(IDictionary)Read(terrain,"active");
        foreach(DictionaryEntry pair in active)
        {
            var region=(TerrainRegionState)pair.Key;var slot=pair.Value;
            var renderer=(MeshRenderer)Read(slot,"Renderer");
            if(!renderer.enabled||!renderer.gameObject.activeInHierarchy)continue;
            var uploader=(TerrainMeshUploader)Read(slot,"Upload");var mesh=uploader.Mesh;
            draws.Add((mesh,renderer.transform.localToWorldMatrix));
            if(!region.Chunk)
            {
                var channels=new List<Vector3>();mesh.GetUVs(1,channels);
                parts.Add(new {chunk=false,x=region.X,z=region.Z,lod=region.TargetLod,overlap=channels.Select(v=>v.z).ToArray()});
                continue;
            }
            var receipt=region.Residency;
            var nodes=new List<object>();
            bool resident=receipt.FirstSupportIndex>=core.Midline.State.Head.Index;
            for(int i=receipt.FirstSupportIndex;resident&&i<=receipt.LastSupportIndex;i++)
            {
                var n=core.Midline.FindCoarse(i);if(n==null)throw new InvalidOperationException("Frozen source no longer resident: "+i);
                var fine=new List<object>();
                for(var f=n.NextFine;f!=null&&f is not CoarseNode;f=f.NextFine)
                    fine.Add(new {bits=Numbers(f.Position.X,f.Position.Y,f.Position.Z,f.Normal.X,f.Normal.Y,f.Normal.Z,f.ShadowLeft,f.ShadowRight)});
                nodes.Add(new {i,bridge=n.Bridge,bits=Numbers(n.Position.X,n.Position.Y,n.Position.Z,n.Normal.X,n.Normal.Y,n.Normal.Z,
                    n.HeadingDelta,n.Grade,n.GradeForwardAverage,n.HeightCorrection,n.Blend,n.Bank,n.ShadowLeft,n.ShadowRight),fine});
            }
            var cells=region.Layout.Cells.Select(c=>new {x=c.X,z=c.Z,inner=c.Inner,candidates=Enumerable.Range(0,c.CandidateCount).Select(c.Candidate).ToArray()}).ToArray();
            var input=new {schema=1,seed=session.settings.seed,lane=(int)core.Config.Mesh.LaneStyle,resolution=config.CellResolution,
                roadWidth=core.Config.Mesh.HalfWidth,negativeLayer=negative,positiveLayer=positive,nodes,cells,
                // Old baked meshes legitimately outlive P5 nodes. The oracle can
                // regenerate those original nodes from seed instead of retaining
                // extra road history in the production implementation.
                nativeNodes=resident?null:new[]{receipt.FirstSupportIndex,receipt.LastSupportIndex}};
            parts.Add(new {chunk=true,id=region.Id,lod=region.TargetLod,input});
        }
        var settings=session.settings;
        File.WriteAllText(Path.Combine(directory,"actual-geometry.json"),JsonConvert.SerializeObject(draws.Select(d=>new {
            position=d.mesh.vertices.SelectMany(Vec).ToArray(),normal=d.mesh.normals.SelectMany(Vec).ToArray(),indices=d.mesh.GetIndices(0),matrix=Matrix(d.matrix)})));
        var maps=new List<object>();
        var names=new[]{"grassMap","sandMap","rockMap","rockMapBump","gravelMap","forestMap","heatherMap","detailNearMap","detailFarMap","grassVariationMap","offroadMap","fadeFine","displacementMap"};
        for(int season=0;season<4;season++)
            maps.Add(names.Select(name=>{var texture=(Texture2D)settings.terrainMaterials[season].GetTexture(name);var path=AssetDatabase.GetAssetPath(texture);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);return new {name,path,srgb=importer.sRGBTexture,width=texture.width,height=texture.height,
                    filter=texture.filterMode.ToString(),aniso=texture.anisoLevel,format=texture.format.ToString()};}).ToArray());
        var target=session.Observer.transform.position-Vector3.up;var forward=session.Observer.transform.forward;forward.y=0;forward.Normalize();var right=Vector3.Cross(Vector3.up,forward);
        var views=new[]{("follow",-forward*18+Vector3.up*10,forward*18), ("shoulder",right*14-forward*6+Vector3.up*6,forward*10),
            ("hills",right*100-forward*80+Vector3.up*100,forward*200-Vector3.up*10), ("overlap",right*45+Vector3.up*50,forward*120)};
        const int width=512,heightPixels=320;
        var cameraObject=new GameObject("T5 fixed-view capture");var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;
        camera.fieldOfView=55;camera.aspect=(float)width/heightPixels;camera.nearClipPlane=.2f;camera.farClipPlane=1800;
        var rt=new RenderTexture(width,heightPixels,24,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);rt.Create();
        var read=new Texture2D(width,heightPixels,TextureFormat.RGBAFloat,false,true);var old=RenderTexture.active;
        var cmd=new CommandBuffer();var cases=new List<object>();var material=new Material(settings.terrainMaterials[0]);
        var sourceTextures=new Dictionary<string,Texture2D>();
        try
        {
            foreach(var view in views)
            {
                camera.transform.position=target+view.Item2;camera.transform.LookAt(target+view.Item3);
                for(int season=0;season<4;season++)
                for(int encoding=0;encoding<2;encoding++)
                {
                    material.CopyPropertiesFromMaterial(settings.terrainMaterials[season]);material.SetFloat("_SurfaceOnly",1);
                    // Control image separates shader/geometry errors from the
                    // shipping DXT importer. It never changes imported assets.
                    if(encoding==1)foreach(var name in names)
                    {
                        var source=(Texture2D)material.GetTexture(name);var path=AssetDatabase.GetAssetPath(source);
                        if(!sourceTextures.TryGetValue(path,out var texture))
                        {
                            bool srgb=((TextureImporter)AssetImporter.GetAtPath(path)).sRGBTexture;
                            texture=new Texture2D(2,2,TextureFormat.RGBA32,false,!srgb);texture.LoadImage(File.ReadAllBytes(path));
                            texture.filterMode=source.filterMode;texture.wrapMode=source.wrapMode;texture.anisoLevel=source.anisoLevel;
                            sourceTextures.Add(path,texture);
                        }
                        material.SetTexture(name,texture);
                    }
                    material.SetVector("_RenderOrigin",origin);material.SetFloat("vehicleIndex",(float)road.ObserverProgress);
                    material.SetFloat("sinkDist",(float)terrain.Core.Schedule.L2);material.SetFloat("offroad",core.Config.Mesh.OffRoad?1:0);
                    material.SetFloat("greenlane",core.Config.Mesh.LaneStyle==SlowRoads.Generation.Road.HillsLaneStyle.OffRoad?1:0);
                    // SetViewProjectionMatrices takes the logical projection and
                    // performs backend conversion. Pre-converting with GetGPUProjectionMatrix
                    // would apply it twice and invert depth in this raw draw path.
                    cmd.Clear();cmd.SetRenderTarget(rt);cmd.ClearRenderTarget(true,true,Color.clear);
                    cmd.SetGlobalVector("_WorldSpaceCameraPos",camera.transform.position);
                    cmd.SetViewProjectionMatrices(camera.worldToCameraMatrix,camera.projectionMatrix);
                    cmd.SetInvertCulling(false);
                    foreach(var draw in draws)cmd.DrawMesh(draw.mesh,draw.matrix,material,0,0);
                    cmd.SetInvertCulling(false);
                    Graphics.ExecuteCommandBuffer(cmd);RenderTexture.active=rt;read.ReadPixels(new Rect(0,0,width,heightPixels),0,0);read.Apply();
                    // Logical camera projection yields bottom-up ReadPixels data,
                    // the same canonical layout as WebGL readPixels.
                    var raw=read.GetRawTextureData<byte>().ToArray();
                    var captureName=view.Item1+"-"+season+(encoding==1?"-rgba32":"");File.WriteAllBytes(Path.Combine(directory,captureName+".unity.rgba"),raw);
                    cases.Add(new {name=captureName,season,sourcePixels=encoding==1,width,height=heightPixels,camera=Vec(camera.transform.position),view=Matrix(camera.worldToCameraMatrix),projection=Matrix(camera.projectionMatrix)});
                }
            }
            File.WriteAllText(Path.Combine(directory,"input.json"),JsonConvert.SerializeObject(new {schema=1,seed=settings.seed,viewDistance=settings.detail,
                detail=settings.terrainDetail,lane=(int)core.Config.Mesh.LaneStyle,layers=new[]{negative,positive},origin=Vec(origin),renderOriginDouble=new[]{road.RenderOrigin.X,road.RenderOrigin.Y,road.RenderOrigin.Z},progress=road.ObserverProgress,
                distance=session.Observer.DistanceMetres,observerIndex=core.Midline.State.ObserverIndex,sinkDist=terrain.Core.Schedule.L2,parts,maps,cases},Formatting.None));
        }
        finally
        {
            RenderTexture.active=old;cmd.Release();rt.Release();
            foreach(var obj in new UnityEngine.Object[]{cameraObject,material,rt,read})UnityEngine.Object.DestroyImmediate(obj);
            foreach(var texture in sourceTextures.Values)UnityEngine.Object.DestroyImmediate(texture);
        }
        return directory+"; "+draws.Count+" regions, "+cases.Count+" fixed views; Editor paused";
    }
}
