using System;
using System.Collections.Generic;
using SlowRoads.Generation.Midline;
using SlowRoads.Generation.Spatial;

namespace SlowRoads.Generation.Contents.Fields
{
    /// <summary>Original module-wide Ka scratch i.nodeLookup (L44162/L44177).
    /// A cached projection can read the last lookup from a different tile or reclip.</summary>
    public sealed class FieldExecutionState {internal int? LastLookupIndex;}
    // Cached projection values never retain pooled road nodes across a tile's lifetime.
    internal sealed class FieldNodeProjection
    {public int Index;public double Side,Distance;public bool Drawn;}
    public readonly struct FieldTree
    {
        public readonly FieldId Field;public readonly int Edge,Ordinal,Type,Variant;public readonly Double3 Position;
        public readonly double Rotation,Scale,Density;public readonly bool HasDensity;
        public FieldTree(FieldId field,int edge,int ordinal,Double3 position,double rotation,double scale,int type,int variant,double? density)
        {Field=field;Edge=edge;Ordinal=ordinal;Position=position;Rotation=rotation;Scale=scale;Type=type;Variant=variant;HasDensity=density.HasValue;Density=density??0;}
    }
    public enum FieldBoundaryOperation {Start,Point,End,Pillar}
    public readonly struct FieldBoundaryEvent
    {
        public readonly FieldId Field;public readonly int Edge;public readonly FieldBoundaryOperation Operation;
        public readonly Double3 Position,Normal;public readonly double U,Shadow;
        public FieldBoundaryEvent(FieldId field,int edge,FieldBoundaryOperation op,Double3 position,Double3 normal,double u,double shadow)
        {Field=field;Edge=edge;Operation=op;Position=position;Normal=normal;U=u;Shadow=shadow;}
    }
    /// <summary>A finite road/height lease supplied by the existing P5/T4 coordinator.
    /// No method may generate road support synchronously. Missing support cancels publication.</summary>
    public interface IFieldWorld
    {
        CoarseNode ClosestNode(int tileX,int tileZ,double x,double z,double radius);
        bool CloseToRoad(int tileX,int tileZ,double x,double z,double radius);
        CoarseNode Node(int index);
        double RawHeight(double x,double z);
        double RoadsideHeight(double x,double z,CoarseNode node);
        double Density(double x,double z);
        double Sidewall(int index,bool left);
        void WriteSidewall(FieldId field,int index,bool left,double offset);
        bool AddTree(in FieldTree tree);
        void WriteShadow(FieldId field,CoarseNode node,double side,double value);
        void WriteColour(FieldId field,double x,double z,double colour);
    }
    public sealed class FieldBoundaryRecipe
    {
        public readonly int TileX,TileZ,Generation,Owner;
        public readonly double WorldX,WorldZ;
        internal readonly List<FieldBoundaryEvent> events=new();internal readonly List<FieldTree> trees=new();
        public IReadOnlyList<FieldBoundaryEvent> Events {get;}public IReadOnlyList<FieldTree> Trees {get;}
        public FieldBoundaryRecipe(int tileX,int tileZ,double tileSize,int generation,int owner)
        {TileX=tileX;TileZ=tileZ;WorldX=tileX*tileSize;WorldZ=tileZ*tileSize;Generation=generation;Owner=owner;Events=events.AsReadOnly();Trees=trees.AsReadOnly();}
    }
}
