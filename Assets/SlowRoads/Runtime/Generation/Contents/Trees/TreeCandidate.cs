using System;
namespace SlowRoads.Generation.Contents.Trees
{
    // Logical address is independent of GPU slots and floating origins. Generation
    // is carried by the store token, so fixed-seed reset histories remain comparable.
    public readonly struct TreeId : IEquatable<TreeId>
    {
        public readonly int TileX,TileZ,Event,CellX,CellZ,Slot;
        public readonly long Source;
        public TreeId(int tx,int tz,int ev,int cx,int cz,int slot,long source=0)
        {TileX=tx;TileZ=tz;Event=ev;CellX=cx;CellZ=cz;Slot=slot;Source=source;}
        public bool Equals(TreeId b)=>TileX==b.TileX&&TileZ==b.TileZ&&Event==b.Event&&CellX==b.CellX&&CellZ==b.CellZ&&Slot==b.Slot&&Source==b.Source;
        public override bool Equals(object b)=>b is TreeId id&&Equals(id);
        public override int GetHashCode()=>HashCode.Combine(TileX,TileZ,Event,CellX,CellZ,Slot,Source);
        public override string ToString()=>$"{TileX}/{TileZ}@{Event}:{CellX}/{CellZ}:{Slot}:{Source}";
    }
    public readonly struct TreeCandidate
    {
        public readonly TreeId Id;
        public readonly double X,Y,Z,Rotation,Scale,Density,GroupScale;
        public readonly int Type,Variant,RawIndex,Bucket;
        public readonly bool FarValid;
        public TreeCandidate(TreeId id,double x,double y,double z,double rotation,double scale,int type,int variant,double density,int rawIndex,int bucket=0,bool farValid=true,double groupScale=1)
        {Id=id;X=x;Y=y;Z=z;Rotation=rotation;Scale=scale;Type=type;Variant=variant;Density=density;RawIndex=rawIndex;Bucket=bucket;FarValid=farValid;GroupScale=groupScale;}
        public TreeCandidate AtHeight(double y)=>new(Id,X,y,Z,Rotation,Scale,Type,Variant,Density,RawIndex,Bucket,FarValid,GroupScale);
    }
    public readonly struct TreeGroup
    {
        public readonly int Count,Arrangement,Type,Band,VariantRow;
        public readonly double Density,NearDensity;
        public TreeGroup(int count,int arrangement,int type,int band,int variantRow,double density,double nearDensity)
        {Count=count;Arrangement=arrangement;Type=type;Band=band;VariantRow=variantRow;Density=density;NearDensity=nearDensity;}
        public int Variant(int slot)=>TreeArrangementTable.Variant(Type,Band,VariantRow,slot);
    }
}
