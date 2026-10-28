using System;

namespace SlowRoads.Generation.Midline
{
    public readonly struct MidlineObserverInput
    {
        public readonly int UpdateOrdinal,Index;
        public MidlineObserverInput(int updateOrdinal,int index){UpdateOrdinal=updateOrdinal;Index=index;}
    }
    /// <summary>Cold-path, logical-boundary replay checkpoint. No height cache dump or
    /// per-instruction persistence. Restores through the same generator and source identity.</summary>
    public sealed class MidlineReplayCheckpoint
    {
        // SHA256 of ordered "key:rawSHA256" lines: Dev,Hills,Settings,Rand,Normals.
        public const string PinnedSourceIdentity="slowroads-1.0.2:742db61e22b2a31ba0691b079d5175ead3a94b7d0fe02590267a5f94669d6bcc";
        readonly MidlineObserverInput[] inputs;
        public int Schema=>1;
        public string SourceIdentity {get;}
        public MidlineConfig Config {get;}
        public string Seed {get;}
        public string HeightSeed {get;}
        public int StartNode {get;}
        public int Updates {get;}
        public ulong Digest {get;}
        public int InputCount=>inputs.Length;
        public MidlineObserverInput GetInput(int i)=>inputs[i];
        internal MidlineReplayCheckpoint(MidlineConfig config,string seed,string heightSeed,int startNode,
            int updates,MidlineObserverInput[] inputs,ulong digest)
        {Config=config;Seed=seed;HeightSeed=heightSeed;StartNode=startNode;Updates=updates;
            this.inputs=inputs;Digest=digest;SourceIdentity=PinnedSourceIdentity;}

        public MidlineSession Restore(Func<MidlineConfig,string,IMidlineHeightSource> sourceFactory,
            int budget=64,string sourceIdentity=PinnedSourceIdentity)
        {
            if(sourceFactory==null)throw new ArgumentNullException(nameof(sourceFactory));
            if(budget<1)throw new ArgumentOutOfRangeException(nameof(budget));
            if(sourceIdentity!=SourceIdentity)throw new InvalidOperationException("Checkpoint source identity mismatch.");
            var height=sourceFactory(Config,HeightSeed)??throw new InvalidOperationException("No replay height source.");
            var session=new MidlineSession(Config,HeightSeed,height,ownsHeight:true);
            try
            {
                session.Begin(Seed,StartNode);int input=0;
                while(session.UpdateOrdinal<Updates)
                {
                    if(session.AtUpdateBoundary && input<inputs.Length && inputs[input].UpdateOrdinal==session.UpdateOrdinal)
                        session.SetObserverIndex(inputs[input++].Index);
                    // Budgets only change yield points, never logical update inputs.
                    while(session.Advance(budget)==MidlineAdvanceResult.Yielded){}
                }
                if(session.ComputeDigest()!=Digest)throw new InvalidOperationException("Replay checkpoint digest mismatch.");
                return session;
            }
            catch {session.Dispose();throw;}
        }
    }
}
