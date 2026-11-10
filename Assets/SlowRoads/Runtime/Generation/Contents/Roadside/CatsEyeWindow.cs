using System;
using SlowRoads.Generation.Midline;
namespace SlowRoads.Generation.Contents.Roadside
{
    /// <summary>x7's two typed-array geometries. Upload only on cycle and initial load;
    /// reuse does not clear the previous tail. Source never changes draw range.</summary>
    public sealed class CatsEyeWindow
    {
        public sealed class Slot
        {
            public readonly int Id;public readonly float[] Positions,Normals;
            public Double3 Origin {get;internal set;}public bool Visible {get;internal set;}=true;
            public int? RetireIndex {get;internal set;}public int Written {get;internal set;}
            internal Slot(int id,int count){Id=id;Positions=new float[count*3];Normals=new float[count*3];}
        }
        public readonly Slot[] Slots;public int Current {get;private set;}
        public event Action<Slot> Uploaded;public event Action<Slot> VisibilityChanged;
        int offset;readonly int max;
        public CatsEyeWindow(double spacing,Double3 start)
        {if(spacing<=0||!double.IsFinite(spacing))throw new ArgumentOutOfRangeException();int count=(int)Math.Ceiling(400/spacing)*2;max=count*3;Slots=new[]{new Slot(0,count),new Slot(1,count)};Slots[0].Origin=start;}
        public void Add(CatsEyePoint p,CoarseNode node)
        {
            var s=Slots[Current];s.Positions[offset]=(float)(p.Position.X-s.Origin.X);s.Normals[offset++]=(float)p.Normal.X;
            s.Positions[offset]=(float)(p.Position.Y-s.Origin.Y);s.Normals[offset++]=(float)p.Normal.Y;
            s.Positions[offset]=(float)(p.Position.Z-s.Origin.Z);s.Normals[offset++]=(float)p.Normal.Z;s.Written=offset/3;
            if(p.Side!=-1||offset<=max-6)return;
            s.Visible=true;s.RetireIndex=node.Index+3;Uploaded?.Invoke(s);VisibilityChanged?.Invoke(s);
            Current=1-Current;offset=0;Slots[Current].Origin=node.Position;Slots[Current].Written=0;
        }
        public void CompleteNode(int observer)
        {var s=Slots[Current];if(s.RetireIndex.HasValue&&s.RetireIndex<=observer&&s.Visible){s.Visible=false;VisibilityChanged?.Invoke(s);}}
        public void PublishInitial(){foreach(var s in Slots)Uploaded?.Invoke(s);}
    }
}
