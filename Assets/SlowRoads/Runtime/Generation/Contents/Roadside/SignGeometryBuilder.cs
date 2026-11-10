using System;

namespace SlowRoads.Generation.Contents.Roadside
{
    /// <summary>h4.makeProtoGeo/setVariant, Settings L46736–46871. Float32 conversion
    /// occurs at the original attribute write; face/pole share the archived 6x2 atlas.</summary>
    public sealed class SignGeometryData
    {
        public readonly float[] Positions,UV;public readonly int[] Indices;
        internal SignGeometryData(float[] positions,float[] uv,int[] indices){Positions=positions;UV=uv;Indices=indices;}
    }
    public static class SignGeometryBuilder
    {
        public static SignGeometryData Build(HillsRoadSign type)
        {
            int variant=(int)type;if((uint)variant>=10)throw new ArgumentOutOfRangeException(nameof(type));
            const double half=.7/2,height=1.8,span=.7,thickness=.04,bottom=-.2,w=1.0/6,v=.5;
            double[] points={-half,height+span,thickness,half,height+span,thickness,-half,height,thickness,half,height,thickness,
                half,height+span,thickness,-half,height+span,thickness,half,height,thickness,-half,height,thickness,
                thickness,height+half,thickness-.01,-thickness,height+half,thickness-.01,0,height+half,-thickness,
                thickness,bottom,thickness-.01,-thickness,bottom,thickness-.01,0,bottom,-thickness};
            double[] uv={w,v*2,w*2,v*2,w,v,w*2,v,0,v*2,w,v*2,0,v,w,v,.06,.48,.08,.48,.1,.48,.06,0,.08,0,.1,0};
            int row=variant%2==0?1:0,column=1+variant/2;
            uv[0]=w*column;uv[1]=v*(row+1);uv[2]=w*(column+1);uv[3]=v*(row+1);
            uv[4]=w*column;uv[5]=v*row;uv[6]=w*(column+1);uv[7]=v*row;
            if(type==HillsRoadSign.ChevronLeft||type==HillsRoadSign.ChevronRight)
            {uv[8]=0;uv[9]=v;uv[10]=w;uv[11]=v;uv[12]=0;uv[13]=0;uv[14]=w;uv[15]=0;}
            else
            {
                uv[0]=w*column+.001;uv[2]=w*(column+1)-.001;uv[4]=w*column+.001;uv[6]=w*(column+1)-.001;
                uv[8]=0;uv[9]=v*2;uv[10]=w;uv[11]=v*2;uv[12]=0;uv[13]=v;uv[14]=w;uv[15]=v;
            }
            var p=new float[points.Length];var u=new float[uv.Length];for(int i=0;i<p.Length;i++)p[i]=(float)points[i];for(int i=0;i<u.Length;i++)u[i]=(float)uv[i];
            return new(p,u,new[]{0,2,1,1,2,3,4,6,5,5,6,7,8,9,11,11,9,12,9,10,12,12,10,13,10,8,13,13,8,11});
        }
    }
}
