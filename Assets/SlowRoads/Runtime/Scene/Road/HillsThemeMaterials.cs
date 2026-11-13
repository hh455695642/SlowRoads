using System;
using System.Collections.Generic;
using SlowRoads.Rendering.Environment;
using UnityEngine;

namespace SlowRoads.Scene.Road
{
    /// <summary>One bounded set of shared display materials per Hills session. Never visits instances.</summary>
    public sealed class HillsThemeMaterials : IDisposable
    {
        sealed class Entry
        {
            public Material Material;
            public bool Owned, Water;
            public readonly List<(string name, float value)> Floats = new();
            public readonly List<(string name, Vector4 value)> Vectors = new();
        }
        readonly Dictionary<(Material, Shader), Material> clones = new();
        readonly Dictionary<Material, Entry> entries = new();
        HillsEnvironmentProfile profile;
        HillsThemeLighting values;
        double shadow;
        bool enabled, disposed;
        public int Count => entries.Count;
        public long ParameterWrites { get; private set; }

        public HillsThemeMaterials(HillsEnvironmentProfile initial, bool lighting = true) => Apply(initial, 0, lighting);

        // Explicit author action: restore only managed parameters, without replacing materials/textures.
        public void Reapply()
        {
            if(disposed)throw new ObjectDisposedException(nameof(HillsThemeMaterials));
            foreach(var entry in entries.Values)Write(entry,true);
        }

        public void Apply(HillsEnvironmentProfile next, double observerShadow, bool lighting = true)
        {
            if (disposed) throw new ObjectDisposedException(nameof(HillsThemeMaterials));
            if (ReferenceEquals(profile,next) && shadow == observerShadow && enabled == lighting) return;
            var evaluated = HillsThemeLighting.Evaluate(next,observerShadow,lighting);
            bool themeChanged = !ReferenceEquals(profile,next) || enabled != lighting;
            profile=next;shadow=observerShadow;enabled=lighting;values=evaluated;
            foreach(var entry in entries.Values) Write(entry,themeChanged);
        }

        public Material Resolve(Material source, Shader shader = null)
        {
            if (disposed) throw new ObjectDisposedException(nameof(HillsThemeMaterials));
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (!source.HasProperty("_ThemeLightingEnabled") && source.shader.name != "SlowRoads/Hills/Water") return source;
            var key=(source,shader);
            if(clones.TryGetValue(key,out var existing)) return existing;
            var material=new Material(source){name=source.name+" (Hills theme)",hideFlags=HideFlags.DontSave};
            if(shader!=null){material.shader=shader;material.enableInstancing=true;}
            try { Register(material,true);clones.Add(key,material);return material; }
            catch { Destroy(material);throw; }
        }

        // Existing terrain owns its material. Call only after CopyPropertiesFromMaterial.
        public void RefreshBorrowed(Material material)
        {
            if(disposed)throw new ObjectDisposedException(nameof(HillsThemeMaterials));
            entries.Remove(material);Register(material,false);
        }
        public void UnregisterBorrowed(Material material)
        {
            if(material!=null && entries.TryGetValue(material,out var entry) && !entry.Owned) entries.Remove(material);
        }
        void Register(Material material,bool owned)
        {
            var entry=new Entry{Material=material,Owned=owned,Water=material.shader.name=="SlowRoads/Hills/Water"};
            foreach(var name in new[]{"shadowFactor","radiance","fresnelIntensity","_ShadowFactor","_Radiance","_FresnelIntensity","_Discolouration","_SunIntensity"})
                if(material.HasProperty(name))entry.Floats.Add((name,material.GetFloat(name)));
            if(entry.Water)foreach(var name in new[]{"_BaseColor","_BodyColor","_HighlightColor"})entry.Vectors.Add((name,material.GetColor(name).linear));
            entries.Add(material,entry);Write(entry,true);
        }
        void Float(Material material,string name,float value)
        { if(material.HasProperty(name)){material.SetFloat(name,value);ParameterWrites++;} }
        void Vector(Material material,string name,Vector4 value)
        {
            if(!material.HasProperty(name))return;
            // Existing water Color properties are authored in sRGB. Theme values are linear;
            // encode once for Unity's Color upload, matching the original water GPU fixture.
            if(name=="_BaseColor"||name=="_BodyColor"||name=="_HighlightColor")material.SetColor(name,((Color)value).gamma);
            else material.SetVector(name,value);
            ParameterWrites++;
        }
        void Write(Entry entry,bool themeChanged)
        {
            var material=entry.Material;
            if(themeChanged)
            {
                Float(material,"_ThemeLightingEnabled",enabled?1:0);
                if(!enabled)
                {
                    foreach(var p in entry.Floats)Float(material,p.name,p.value);
                    foreach(var p in entry.Vectors)Vector(material,p.name,p.value);
                    return;
                }
                Float(material,"_ThemeReferenceSunIntensity",values.ReferenceSunIntensity);
                Vector(material,"_ThemeSun",values.Sun);
                Float(material,"shadowFactor",values.Theme.x);Float(material,"_ShadowFactor",values.Theme.x);
                Float(material,"radiance",values.Theme.y);
                // Hills ls assigns tree radiance /2; the existing shader's *2 and *.5 cancel.
                Float(material,"_Radiance",material.shader.name.StartsWith("SlowRoads/Hills/Tree",StringComparison.Ordinal)?values.Theme.y*.5f:values.Theme.y);
                Float(material,"fresnelIntensity",values.Theme.z);Float(material,"_FresnelIntensity",values.Theme.z);
                Float(material,"_Discolouration",values.Theme.w);Float(material,"_SunIntensity",values.ReferenceSunIntensity);
                if(entry.Water){Vector(material,"_BaseColor",values.WaterBase);Vector(material,"_BodyColor",values.WaterBody);Vector(material,"_HighlightColor",values.WaterHighlight);}
            }
            if(enabled){Vector(material,"_ThemeAmbient",values.Ambient);Vector(material,"_ThemeHemisphere",values.Hemisphere);}
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;
            foreach(var entry in entries.Values)if(entry.Owned)Destroy(entry.Material);
            entries.Clear();clones.Clear();
        }
        static void Destroy(UnityEngine.Object value)
        {if(Application.isPlaying)UnityEngine.Object.Destroy(value);else UnityEngine.Object.DestroyImmediate(value);}
    }
}
