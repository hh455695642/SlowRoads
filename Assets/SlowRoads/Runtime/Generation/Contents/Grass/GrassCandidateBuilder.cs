using System;
using System.Collections.Generic;
using SlowRoads.Generation.Midline;

namespace SlowRoads.Generation.Contents.Grass
{
    /// <summary>s0.loadCustom1 S L46317–46459. One original row per work unit.
    /// CPU decisions and short-circuit random consumption are independent of GPU visibility.</summary>
    public sealed class GrassCandidateBuilder : IDisposable
    {
        readonly GrassCellInput cell;readonly GrassGenerationConfig config;readonly GrassRandomState random;
        readonly List<GrassCandidate> candidates=new();int row;
        readonly Fields.FieldRandomState.Stream.Cursor[] checkpoint;
        Fields.FieldRandomState.Stream.Lease lease;bool disposed;
        public bool Complete {get;private set;}
        public IReadOnlyList<GrassCandidate> Candidates=>candidates;
        public GrassCandidateBuilder(GrassCellInput cell,GrassGenerationConfig config,GrassRandomState random,object transactionOwner=null)
        {
            this.cell=cell??throw new ArgumentNullException(nameof(cell));this.config=config;this.random=random??throw new ArgumentNullException(nameof(random));
            lease=random.Shared.Claim(transactionOwner??this);checkpoint=random.Capture();
        }
        public int Advance(int budget)
        {
            if(disposed)throw new ObjectDisposedException(nameof(GrassCandidateBuilder));if(budget<0)throw new ArgumentOutOfRangeException(nameof(budget));int used=0;
            while(used<budget&&!Complete){used++;if(config.Density==0){Complete=true;break;}Row();}
            if(Complete){lease?.Dispose();lease=null;}return used;
        }
        double Next(Fields.FieldRandomState.Stream stream,string name)=>random.Next(stream,name);
        void Row()
        {
            int x=row;double step=10.0/cell.Resolution,wx=cell.WorldX+x*step;
            if(cell.Inner||x%2==0)
            {
                // Preserve the increment in the loop condition as well as the increment expression.
                for(int z=0;z<cell.Resolution&&!(!cell.Inner&&z>0&&(++z>=cell.Resolution));z++)
                {
                    random.X=x;random.Z=z;var a=cell.At(x,z);double rp=a.RoadProximity,wy=a.Height,wz=cell.WorldZ+z*step;
                    if(config.OffRoad){if(rp<.1&&rp> -1)continue;}
                    else if(rp<(config.WideRoad?.2:.1))continue;
                    if(wy<1.5)continue;var norm=a.Normal;
                    if(norm.Y+Next(random.Slope,"slopeRand")<.7)continue;
                    double tm=a.TreeMask,scale=1,sink=.2,bushProb=0;int variant=0;bool check=true;
                    if(tm<.1&&rp>1.5&&wy>100)
                    {
                        bushProb=random.BushNoise.Get(wx/200,wz/200);double gorseProb=random.BushNoise.Get(wx/1000,wz/1000)*2;
                        double factor=(wy-100)/100;if(factor>.5)factor=.5;
                        if(bushProb<factor&&gorseProb<factor)
                        {scale=1.25+factor-bushProb;bushProb=(wy>200?1:.5+(wy-100)/200)*.8;sink=.1+Next(random.Shared,"G4")*.4;check=false;variant=3;}
                        else bushProb=0;
                    }
                    if(check)
                    {
                        double noise=random.BushNoise.Get(wx/1000,wz/1000);
                        int side=(cell.RoadPosition.X-wx)*cell.RoadNormal.X+(cell.RoadPosition.Z-wz)*cell.RoadNormal.Z<0?-1:1;
                        if(rp>.5&&rp<1.5&&(cell.LeftWall!=0||cell.RightWall!=0)&&(cell.WallSide==0||side==cell.WallSide))bushProb=Math.Max(tm,.75)*noise;
                        else if(tm>.1)bushProb=tm*.65;
                        else
                        {
                            bushProb=.4;if(wy<5)bushProb*=wy/5;else if(wy>205)bushProb=.1;else bushProb=.1+(.3*(205-wy))/200;
                            if(rp>2){if(rp>5)bushProb=0;else bushProb*=(7-rp)/5;}bushProb*=noise;
                        }
                        if(bushProb>0){scale=Next(random.Scale,"scaleRand")+tm/2+variant/6.0;scale*=1.25;}
                    }
                    bushProb*=bushProb;
                    if(rp<1){if(rp<.5)bushProb=0;else bushProb*=(rp-.5)*2;}
                    else if(rp>2){bushProb*=1-(rp-2)/13;if(bushProb<0)bushProb=0;}
                    if(Next(random.Shared,"G4")<bushProb*config.Density*config.Density)
                    {
                        double ox=Next(random.Position,"posRand")*Math.Min(rp+.3,1),oz=Next(random.Position,"posRand")*Math.Min(rp+.3,1);
                        if(rp>2)sink+=(rp-2)*.05;wy-=sink*scale;
                        Add(true,x,z,wx+ox,wy-ox*norm.X-oz*norm.Z,wz+oz,norm,Next(random.Rotation,"rotRand"),scale,tm,rp,variant);continue;
                    }
                    if(rp>config.Margin||((!config.OffRoad||rp>0)&&Next(random.Shared,"G4")>config.Density))continue;
                    scale=1.5;scale*=Next(random.GrassScale,"grassScaleRand");
                    double gx=Next(random.Position,"posRand")*Math.Min(rp+.3,1),gz=Next(random.Position,"posRand")*Math.Min(rp+.3,1);
                    if(rp<2)
                    {
                        if(config.OffRoad){if(rp>0)sink=(2-rp)*.05;else{sink=.3;scale=1.5;gx*=.5;gz*=.5;rp=-1;}}
                        else sink=(2-rp)*.1;
                    }
                    else
                    {
                        sink=0;bool reject=false;
                        if(rp>4){double rpl=(rp-4)/(config.Margin-4);scale+=rpl*(1-tm);sink=rpl*scale*.4;reject=.2+.8*rpl>Next(random.Density,"L9");}
                        if(reject||tm*Math.Min(1,(rp-2)/5)>Next(random.Density,"L9"))continue;
                    }
                    wy-=sink*.8+Next(random.Sink,"sinkRand");
                    Add(false,x,z,wx+gx,wy-gx*norm.X-gz*norm.Z,wz+gz,norm,Next(random.Rotation,"rotRand"),scale,tm,rp,0);
                }
                row++;if(!cell.Inner)row++;if(row<cell.Resolution)return;
            }
            Complete=true;
        }
        void Add(bool bush,int x,int z,double px,double py,double pz,Double3 normal,double rotation,double scale,double shadow,double rp,int variant)
        {candidates.Add(new(bush,x,z,new Double3(px,py,pz),normal,rotation,scale,shadow,rp,cell.RetireIndex,variant));}
        public void Dispose()
        {
            if(disposed)return;disposed=true;
            try{if(!Complete){random.Restore(checkpoint);candidates.Clear();}}
            finally{lease?.Dispose();lease=null;}
        }
    }
}
