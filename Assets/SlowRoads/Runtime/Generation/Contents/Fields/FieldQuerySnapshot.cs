using System;
using System.Collections.Generic;
using SlowRoads.Generation.Midline;

namespace SlowRoads.Generation.Contents.Fields
{
    /// <summary>Explicit C5/C6 read boundary. Copies values only; no shared registry,
    /// pooled road nodes or GPU visibility escape. Capture once per content revision.</summary>
    public sealed class FieldQuerySnapshot
    {
        public readonly ContentVersion Version;
        public readonly IReadOnlyList<FieldPlotSnapshot> Plots;
        public readonly IReadOnlyList<FieldBoundaryEvent> Boundaries;
        public readonly IReadOnlyList<FieldTree> Trees;
        public readonly IReadOnlyDictionary<ContentAttributeKey,double> Attributes;
        internal FieldQuerySnapshot(ContentVersion version,List<FieldPlotSnapshot> plots,List<FieldBoundaryEvent> boundaries,
            List<FieldTree> trees,IReadOnlyDictionary<ContentAttributeKey,double> attributes)
        {Version=version;Plots=plots.AsReadOnly();Boundaries=boundaries.AsReadOnly();Trees=trees.AsReadOnly();Attributes=attributes;}
        public bool IsCurrent(ContentVersion version)=>Version.Equals(version);
        public double Read(ContentAttributeKey key)=>Attributes.TryGetValue(key,out double value)?value:0;
    }
    public sealed class FieldPlotSnapshot
    {
        public readonly FieldId Id;
        public readonly IReadOnlyList<Double3> TemplatePolygon;
        public readonly IReadOnlyList<int> TemplateEdges;
        internal FieldPlotSnapshot(FieldCandidate candidate,FieldTemplate template)
        {
            Id=candidate.Id;var plot=template.Plots[candidate.Index];var points=new Double3[plot.Nodes.Length];
            for(int i=0;i<points.Length;i++){int at=plot.Nodes[i];var p=template.Nodes[at];points[i]=new Double3(candidate.Tile.WorldX+p.X,candidate.Tile.Heights[at],candidate.Tile.WorldZ+p.Z);}
            TemplatePolygon=Array.AsReadOnly(points);TemplateEdges=Array.AsReadOnly((int[])plot.Edges.Clone());
        }
    }
}
