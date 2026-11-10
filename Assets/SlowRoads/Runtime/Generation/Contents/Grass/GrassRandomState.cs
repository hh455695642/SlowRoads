using System;
using SlowRoads.Generation.Contents.Fields;
using SlowRoads.Generation.Contents.Trees;

namespace SlowRoads.Generation.Contents.Grass
{
    public readonly struct GrassRandomCall
    {
        public readonly string Name; public readonly int X,Z,Before,After; public readonly double Value;
        internal GrassRandomCall(string name,int x,int z,int before,int after,double value)
        {Name=name;X=x;Z=z;Before=before;After=after;Value=value;}
    }
    /// <summary>S L46125/L46143–46153, R L00006–18. G4 is borrowed from C2, never reseeded here.</summary>
    public sealed class GrassRandomState
    {
        public readonly FieldRandomState.Stream Shared,Density,Position,Rotation,Scale,GrassScale,Slope,Sink,Elevation;
        public readonly HillsTreeVariantNoise BushNoise=new(3,9),SecondaryBushNoise=new(3,3);
        public Action<GrassRandomCall> Trace;
        internal int X,Z;
        public GrassRandomState(FieldRandomState.Stream shared,double step,FieldRandomState.Stream density=null)
        {
            Shared=shared??throw new ArgumentNullException(nameof(shared));
            Density=density??new("a");Position=new("slowroads",101,step,step/2);Rotation=new("slowroads",59,Math.PI*2);
            Scale=new("slowroads",101,.9,-.3);GrassScale=new("slowroads",101,.4,-.8);Slope=new("slowroads",59,.1,.05);
            Sink=new("slowroads",101,.15,.05);Elevation=new("elev",101,25);
        }
        internal double Next(FieldRandomState.Stream stream,string name)
        {int before=stream.Index;double value=stream.Next();Trace?.Invoke(new(name,X,Z,before,stream.Index,value));return value;}
        public FieldRandomState.Stream.Cursor[] Capture()=>new[]{Shared.Capture(),Density.Capture(),Position.Capture(),Rotation.Capture(),Scale.Capture(),GrassScale.Capture(),Slope.Capture(),Sink.Capture(),Elevation.Capture()};
        internal void Restore(FieldRandomState.Stream.Cursor[] checkpoint)
        {
            var streams=new[]{Shared,Density,Position,Rotation,Scale,GrassScale,Slope,Sink,Elevation};
            if(checkpoint==null||checkpoint.Length!=streams.Length)throw new ArgumentException(nameof(checkpoint));
            for(int i=0;i<streams.Length;i++)streams[i].Restore(checkpoint[i]);
        }
    }
}
