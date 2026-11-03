using System;
using System.Collections.Generic;

namespace SlowRoads.Generation.Contents.Fields
{
    public readonly struct FieldId : IEquatable<FieldId>
    {
        public readonly int X,Z,Plot;public FieldId(int x,int z,int plot){X=x;Z=z;Plot=plot;}
        public bool Equals(FieldId other)=>X==other.X&&Z==other.Z&&Plot==other.Plot;
        public override bool Equals(object obj)=>obj is FieldId other&&Equals(other);
        public override int GetHashCode()=>HashCode.Combine(X,Z,Plot);
    }
    public readonly struct FieldCandidate
    {
        public readonly FieldTileRegistry.Tile Tile;public readonly int Index;
        public FieldId Id=>new(Tile.X,Tile.Z,Index);
        internal FieldCandidate(FieldTileRegistry.Tile tile,int index){Tile=tile;Index=index;}
    }
    /// <summary>Ka.checkFieldInclusions L44094–44128. A unit is one original field tile.
    /// The coordinator serializes jobs sharing the registry/random state; visibility never enters this algorithm.</summary>
    public sealed class FieldCandidateBuilder
    {
        readonly FieldTemplate template;readonly FieldTileRegistry.Tile[] tiles;readonly FieldRandomState random;
        readonly Func<double,double,double> height,density;readonly FieldQualityNoise quality;
        readonly double minX,minZ,maxX,maxZ;readonly int densityOption;int cursor;
        readonly List<FieldCandidate> candidates=new();
        public IReadOnlyList<FieldCandidate> Candidates=>candidates;
        public bool Complete=>cursor>=tiles.Length;
        public FieldCandidateBuilder(FieldTemplate template,FieldTileRegistry.Tile[] tiles,FieldRandomState random,FieldQualityNoise quality,
            double x,double z,double tileSize,int densityOption,Func<double,double,double> height,Func<double,double,double> density)
        {this.template=template;this.tiles=tiles;this.random=random;this.quality=quality;minX=x;minZ=z;maxX=x+tileSize;maxZ=z+tileSize;
            this.densityOption=densityOption;this.height=height;this.density=density;}
        public bool Step()
        {
            if(Complete)return true;var tile=tiles[cursor++];
            for(int i=0;i<template.Plots.Length;i++)
            {
                var plot=template.Plots[i];double x=plot.CenterX+tile.WorldX,z=plot.CenterZ+tile.WorldZ;
                if(tile.DrawnFields[i]||x<minX||x>maxX||z<minZ||z>maxZ||density(x,z)>.15)continue;
                double hi=-9007199254740991,lo=9007199254740991;
                foreach(int node in plot.Nodes)
                {
                    double h=tile.Heights[node];
                    // JS truthiness is intentional: a cached zero consumes Density again.
                    if(h==0||double.IsNaN(h)){var p=template.Nodes[node];h=height(tile.WorldX+p.X,tile.WorldZ+p.Z);tile.Heights[node]=h;tile.Densities[node]=random.TreeDensity(h,densityOption);}
                    if(h>hi)hi=h;if(h<lo)lo=h;
                }
                double grade=(hi-lo)/plot.Radius;if(lo<15||hi>220||grade>.7)continue;
                double avg=(lo+hi)/2,hq=1-avg/220,gq=1-grade/.7;
                double probability=(quality.GetXZ(x,z)+(hq+gq)/2)/2;if(probability<.5)continue;
                tile.DrawnFields[i]=true;candidates.Add(new FieldCandidate(tile,i));
            }
            return Complete;
        }
    }
}
