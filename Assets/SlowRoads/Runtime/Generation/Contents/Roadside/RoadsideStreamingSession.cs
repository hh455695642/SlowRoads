using System;
using System.Collections.Generic;
using SlowRoads.Generation.Midline;
using SlowRoads.Generation.Streaming;
using SlowRoads.Generation.Terrain;
using SlowRoads.Generation.Spatial;
using SlowRoads.Generation.Contents.Walls;

namespace SlowRoads.Generation.Contents.Roadside
{
    public enum RoadsideKind {Sign,VergeMarker}
    public sealed class RoadsideObject
    {
        public readonly string Id;public readonly RoadsideKind Kind;public readonly int NodeIndex;public readonly double RetireIndex;
        public readonly Double3 Position;public readonly double Rotation;public readonly HillsRoadSign Sign;public readonly int Revision;
        internal RoadsideObject(string id,RoadsideKind kind,int node,double retire,Double3 position,double rotation,HillsRoadSign sign=default,int revision=0)
        {Id=id;Kind=kind;NodeIndex=node;RetireIndex=retire;Position=position;Rotation=rotation;Sign=sign;Revision=revision;}
    }
    /// <summary>One bounded P5/T4 consumer. _9 + E7 + x7, S L45307-45446/L46482-46723.
    /// No RNG, camera, vehicle, terrain scheduler or alternative road projection.</summary>
    public sealed class RoadsideStreamingSession : IDisposable
    {
        readonly RoadStreamingSession road;readonly TerrainStreamingSession terrain;readonly WallStreamingSession walls;
        readonly HillsRoadBlend blend;public readonly RoadsideObjectWindow Objects=new();
        readonly Dictionary<int,RoadSignRecord> pending=new();readonly Dictionary<WallId,string> markers=new();
        readonly Dictionary<string,int> signIds=new();
        readonly List<int> completed=new();RoadSignGenerator signs;CatsEyeGenerator eyes;int generation=-1,checkedObserver=int.MinValue,initialObserver;bool disposed,initialEyes;
        public RoadsideGenerationConfig Config {get;}
        public CatsEyeWindow EyeWindow {get;private set;}
        public event Action<RoadsideObject> Committed;public event Action<string> Released;
        public event Action<CatsEyeWindow.Slot> EyesUploaded;public event Action<CatsEyeWindow.Slot> EyesVisibilityChanged;
        public event Action Reset;
        public IEnumerable<RoadsideObject> Active=>Objects.Active;public int Count=>Objects.Count;public int PendingCount=>pending.Count;
        public int SignRoot=>signs?.RootIndex??int.MinValue;public int EyeIndex=>eyes?.NextIndex??int.MinValue;
        public bool InitialReady=>signs!=null&&signs.RootIndex>=initialObserver+Config.SignLookahead&&eyes.NextIndex>=initialObserver+56;
        public bool HasWork=>road.Ready&&(signs==null||signs.RootIndex<road.Midline.State.ObserverIndex+Config.SignLookahead||eyes.NextIndex<road.Midline.State.ObserverIndex+56);
        public RoadsideStreamingSession(RoadStreamingSession road,TerrainStreamingSession terrain,WallStreamingSession walls)
        {
            this.road=road??throw new ArgumentNullException(nameof(road));this.terrain=terrain??throw new ArgumentNullException(nameof(terrain));this.walls=walls??throw new ArgumentNullException(nameof(walls));
            Config=new(road.Config);blend=new(road.Config.Mesh.LaneStyle);walls.Committed+=WallCommitted;walls.Released+=WallReleased;road.Invalidated+=Clear;
            Objects.Committed+=v=>Committed?.Invoke(v);Objects.Released+=id=>{if(signIds.Remove(id,out int sign)){pending.Remove(sign);signs?.Retire(sign);}Released?.Invoke(id);};
        }
        string Prefix=>$"{road.Midline.State.Generation}/{road.ContentEpoch}/";
        void Put(RoadsideObject value)
        {
            Objects.Put(value);
        }
        void Remove(string id)=>Objects.Remove(id);
        void NativeObjectUpdate(int observer)
        {
            // E7 retires at most one expired object plus one over the 30-object cap per
            // logical observer tick. Budget slices must never repeat this side effect.
            Objects.Update(observer);
            signs?.DiscardHistoryThrough(observer);
        }
        void SignChanged(RoadSignRecord r,bool created)
        {if(created||pending.ContainsKey(r.Id)||Objects.Contains(Prefix+"sign/"+r.Id))pending[r.Id]=r;}
        void WallCommitted(StreamedWall w)
        {
            if(!w.Geometry.Marker.HasValue){WallReleased(w.Id);return;}var m=w.Geometry.Marker.Value;
            string id=Prefix+"marker/"+w.Id;markers[w.Id]=id;
            Put(new(id,RoadsideKind.VergeMarker,m.NodeIndex,m.NodeIndex+10,m.Position,m.Rotation));
        }
        void WallReleased(WallId id){if(markers.Remove(id,out var key))Remove(key);}
        bool Resolve(RoadSignRecord r,out RoadsideObject result)
        {
            result=null;var p=r.Position;
            if(road.Config.Mesh.OffRoad)
            {
                var n=road.Midline.FindCoarse(r.NodeIndex);if(n==null||!terrain.TryLookupHeight(p.X,p.Z,out double h))return false;
                if(!RoadProjection.TryCreate(p.X,p.Z,n,out var projection))return false;
                // Ni.getRoadsideHeight, S L50340-50345. Water clamp precedes Ll;
                // a bridge surface inside the road keeps the existing projected Y.
                bool bridge=road.Midline.FindCoarse(projection.Index)?.Bridge==true;
                double y=RoadsideHeight.Select(h,projection.Y,projection.Distance,bridge,road.Config.Mesh.HalfWidth,out bool applyBlend);
                if(applyBlend)y=blend.Evaluate(y,ref projection,p.X,p.Z);
                p=new(p.X,y-r.Sink,p.Z);
            }
            result=new(Prefix+"sign/"+r.Id,RoadsideKind.Sign,r.NodeIndex,r.RetireIndex,p,r.Rotation,r.Type,r.Revision);return true;
        }
        public int Advance(int budget)
        {
            if(disposed)throw new ObjectDisposedException(nameof(RoadsideStreamingSession));if(budget<0)throw new ArgumentOutOfRangeException();
            if(budget==0||!road.Ready||!road.AtTickBoundary||road.TerrainBuildPending)return 0;
            int observer=road.Midline.State.ObserverIndex,used=0;
            if(generation!=road.Midline.State.Generation)Clear();
            if(signs==null)
            {
                var start=road.Midline.FindCoarse(observer);if(start==null||start.Index+Config.SignLookahead+15>=road.Midline.AttributeReadyExclusive)return 0;
                generation=road.Midline.State.Generation;
                initialObserver=observer;
                signs=new(start,3,road.Config.Mesh.HalfWidth,Config.SignLookahead,Config.SignRetire,deferInitialLookahead:true,initialListener:SignChanged);
                eyes=new(road.Midline.State.Head,Config.EyeSpacing,.04);EyeWindow=new(Config.EyeSpacing,road.Midline.State.Head.Position);
                EyeWindow.Uploaded+=s=>EyesUploaded?.Invoke(s);EyeWindow.VisibilityChanged+=s=>EyesVisibilityChanged?.Invoke(s);
                eyes.Emitted+=EyeWindow.Add;eyes.NodeCompleted+=_=>EyeWindow.CompleteNode(checkedObserver);initialEyes=true;
                foreach(var w in walls.Active)if(w.Geometry!=null)WallCommitted(w);used++;
            }
            if(checkedObserver!=observer){NativeObjectUpdate(observer);checkedObserver=observer;}
            int target=observer+Config.SignLookahead;
            if(used<budget&&road.Midline.AttributeReadyExclusive>target+15)used+=signs.Advance(observer,budget-used);
            if(used<budget)used+=eyes.Advance(Math.Min(observer+56,road.Midline.AttributeReadyExclusive-2),budget-used);
            if(initialEyes&&eyes.NextIndex>=observer+56){EyeWindow.PublishInitial();initialEyes=false;}
            // Off-road height availability may lag T4; defer only placement, never the
            // road producer. Records carry world values, not a temporary Mesh instance ID.
            completed.Clear();foreach(var pair in pending)
            {if(used>=budget)break;if(!Resolve(pair.Value,out var value))continue;signIds[value.Id]=pair.Key;Put(value);completed.Add(pair.Key);used++;}
            foreach(int id in completed)pending.Remove(id);eyes.DiscardHistory();return used;
        }
        void Clear()
        {
            Objects.Clear();pending.Clear();markers.Clear();signIds.Clear();
            signs=null;eyes=null;EyeWindow=null;generation=-1;checkedObserver=int.MinValue;Reset?.Invoke();
        }
        public void Dispose(){if(disposed)return;disposed=true;walls.Committed-=WallCommitted;walls.Released-=WallReleased;road.Invalidated-=Clear;Clear();}
    }
}
