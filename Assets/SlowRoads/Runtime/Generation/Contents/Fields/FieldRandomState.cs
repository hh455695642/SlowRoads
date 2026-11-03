using System;
using SlowRoads.Generation.Random;

namespace SlowRoads.Generation.Contents.Fields
{
    /// <summary>Settings L43851–43860. This is separate from C1's ordinary candidate streams.
    /// Shared is the G4 farm/grass stream; C5 must consume this same instance.</summary>
    public sealed class FieldRandomState
    {
        public sealed class Stream
        {
            readonly double[] values;public int Index {get;private set;}public long Calls {get;private set;}
            internal Stream(string seed,int count=101,double range=1,double subtract=0,bool integer=false)
            {values=new double[count];var rng=new AleaRandom(seed);for(int i=0;i<count;i++){double v=rng.Next()*range-subtract;values[i]=integer?Math.Floor(v):v;}}
            public double Next(){if(Index==values.Length)Index=0;Calls++;return values[Index++];}
            public readonly struct Cursor {public readonly int Index;public readonly long Calls;internal Cursor(int index,long calls){Index=index;Calls=calls;}}
            public Cursor Capture()=>new(Index,Calls);
            internal void Restore(Cursor c){Index=c.Index;Calls=c.Calls;}
        }
        public readonly Stream Shared,Rotation=new("rot",31,Math.PI*2),Scale=new("scale",41,.8,-.7),Type=new("type",51),
            Variant=new("var",39,4,0,true),Placement=new("placement",101),Colour=new("offset",41,1.8,.9),Density=new("treeDensity");
        public FieldRandomState(string seed){Shared=new Stream(seed??throw new ArgumentNullException(nameof(seed)),251);}
        public Stream[] Streams=>new[]{Shared,Rotation,Scale,Type,Variant,Placement,Colour,Density};
        public Stream.Cursor[] Capture(){var streams=Streams;var result=new Stream.Cursor[streams.Length];for(int i=0;i<result.Length;i++)result[i]=streams[i].Capture();return result;}
        public void Restore(Stream.Cursor[] checkpoint)
        {var streams=Streams;if(checkpoint==null||checkpoint.Length!=streams.Length)throw new ArgumentException(nameof(checkpoint));for(int i=0;i<streams.Length;i++)streams[i].Restore(checkpoint[i]);}
        public double TreeDensity(double height,int density)
        {
            double r=Density.Next();if(density!=0){if(density==1)return 0;r*=(density-1)/3.0;}
            return r<.2?0:height<150?.5+r*.5:height<300?(.5+r*.5)*((height-150)/150):0;
        }
        public int TreeType(int count=2)=>count>1&&Type.Next()<.25?1:0;
    }
}
