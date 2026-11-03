using System;
using System.Collections.Generic;

namespace SlowRoads.Generation.Contents.Fields
{
    /// <summary>A serial C2 transaction at the P5/T4 content barrier. Owns its field-tile lease.
    /// Cancellation rolls back unpublished occupancy/random consumption; a published reclip never rewinds streams.</summary>
    public sealed class FieldGenerationJob : IDisposable
    {
        readonly FieldTileRegistry registry;readonly FieldTileRegistry.Tile[] lease;readonly FieldRandomState random;
        FieldTileRegistry.Checkpoint before;FieldRandomState.Stream.Cursor[] randomBefore;
        readonly FieldCandidateBuilder inclusion;IFieldWorld world;
        readonly int x,z,detail,density;int owner;readonly double tileSize,halfWidth;readonly bool colour;
        FieldClipBuilder clipping;bool disposed,published;
        readonly FieldExecutionState execution;int? lookupBefore;
        public bool Complete=>clipping!=null&&clipping.Complete;
        public FieldBoundaryRecipe Recipe=>Complete?clipping.Recipe:null;
        public IReadOnlyList<FieldCandidate> Candidates=>inclusion.Candidates;
        public FieldGenerationJob(FieldTileRegistry registry,FieldRandomState random,FieldQualityNoise quality,IFieldWorld world,
            int x,int z,int owner,double tileSize,double halfWidth,int detail,int density,bool colour,FieldExecutionState execution=null)
        {
            this.registry=registry;this.random=random;this.world=world;this.x=x;this.z=z;this.owner=owner;this.tileSize=tileSize;this.halfWidth=halfWidth;this.detail=detail;this.density=density;this.colour=colour;
            this.execution=execution??new FieldExecutionState();lookupBefore=this.execution.LastLookupIndex;
            lease=registry.Acquire(x*tileSize,z*tileSize,tileSize);before=registry.Capture(lease);randomBefore=random.Capture();
            inclusion=new FieldCandidateBuilder(registry.Template,lease,random,quality,x*tileSize,z*tileSize,tileSize,density,world.RawHeight,world.Density);
        }
        public int Advance(int budget)
        {
            if(disposed)throw new ObjectDisposedException(nameof(FieldGenerationJob));if(budget<0)throw new ArgumentOutOfRangeException(nameof(budget));int used=0;
            while(used<budget&&!Complete)
            {
                if(!inclusion.Complete){inclusion.Step();used++;continue;}
                clipping??=new FieldClipBuilder(registry.Template,inclusion.Candidates,random,world,x,z,tileSize,registry.Generation,owner,null,halfWidth,detail,density,colour,execution);
                clipping.Step();used++;
            }
            return used;
        }
        public void MarkPublished(){if(!Complete||disposed)throw new InvalidOperationException("Incomplete field recipe");published=true;before=null;randomBefore=null;}
        public void Reclip(IFieldWorld world,int owner)
        {
            if(!published||disposed)throw new InvalidOperationException("Reclip requires a stable field tile");
            before=registry.Capture(lease);randomBefore=random.Capture();lookupBefore=execution.LastLookupIndex;this.world=world;
            clipping=new FieldClipBuilder(registry.Template,inclusion.Candidates,random,world,x,z,tileSize,registry.Generation,owner,this.owner,halfWidth,detail,density,colour,execution);
            this.owner=owner;published=false;
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;
            if(!published){registry.Restore(before);random.Restore(randomBefore);execution.LastLookupIndex=lookupBefore;}registry.Release(lease);
        }
    }
}
