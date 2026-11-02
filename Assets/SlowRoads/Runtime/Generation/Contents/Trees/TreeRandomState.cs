using System;
using SlowRoads.Generation.Random;

namespace SlowRoads.Generation.Contents.Trees
{
    /// <summary>Original module-level I9/N9/Va/a7 tables (Rand L00006–00033;
    /// Settings L43758–43760/L44971). Construct once per fresh content session,
    /// never per Tile. Warm map initialization does not reset this object.</summary>
    public sealed class TreeRandomState
    {
        readonly Stream arrangement=new("slowroads",5184,1728,true);
        readonly Stream variant=new("slowroads",31,4,true);
        readonly Stream count=new("slowroads",101,1,false);
        readonly Stream bucket=new("a",101,1,false);
        public int NextArrangement()=>(int)arrangement.Next();
        public int NextVariant()=>(int)variant.Next();
        public double NextCount()=>count.Next();
        public double NextBucket()=>bucket.Next();
        public Checkpoint Capture()=>new(arrangement.Capture(),variant.Capture(),count.Capture(),bucket.Capture());
        public void Restore(Checkpoint state)
        {
            // Validate the complete transaction before changing any stream.
            arrangement.Validate(state.Arrangement);variant.Validate(state.Variant);count.Validate(state.Count);bucket.Validate(state.Bucket);
            arrangement.Restore(state.Arrangement);variant.Restore(state.Variant);count.Restore(state.Count);bucket.Restore(state.Bucket);
        }
        public readonly struct Cursor
        {
            public readonly int Index;public readonly long Calls;
            public Cursor(int index,long calls){Index=index;Calls=calls;}
        }
        public readonly struct Checkpoint
        {
            public readonly Cursor Arrangement,Variant,Count,Bucket;
            public Checkpoint(Cursor arrangement,Cursor variant,Cursor count,Cursor bucket)
            {Arrangement=arrangement;Variant=variant;Count=count;Bucket=bucket;}
        }
        sealed class Stream
        {
            readonly double[] values;int index;long calls;
            public Stream(string seed,int length,double range,bool integer)
            {
                var random=new AleaRandom(seed);values=new double[length];
                for(int i=0;i<length;i++){double value=random.Next()*range;values[i]=integer?Math.Floor(value):value;}
            }
            public double Next(){if(index>=values.Length)index=0;calls++;return values[index++];}
            public Cursor Capture()=>new(index,calls);
            public void Validate(Cursor value)
            {
                if(value.Calls<0||value.Index!=(value.Calls==0?0:(int)((value.Calls-1)%values.Length)+1))
                    throw new ArgumentException("Invalid original cyclic random cursor");
            }
            public void Restore(Cursor value){index=value.Index;calls=value.Calls;}
        }
    }
}
