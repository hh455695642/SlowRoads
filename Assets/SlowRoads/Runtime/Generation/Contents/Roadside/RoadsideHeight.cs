namespace SlowRoads.Generation.Contents.Roadside
{
    public static class RoadsideHeight
    {
        /// <summary>Ni.getRoadsideHeight S L50340-50345, off-road consumer branch.
        /// Returns the source input to existing Ll, or the final bridge/water height.</summary>
        public static double Select(double raw,double projected,double distance,bool bridge,double halfWidth,out bool applyBlend)
        {
            applyBlend=false;if(bridge&&distance<halfWidth)return projected;if(raw<0)return 0;applyBlend=true;return raw;
        }
    }
}
