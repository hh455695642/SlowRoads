namespace SlowRoads.Generation.Contents.Trees
{
    public static class TreeSurfaceShadow
    {
        // s0 L46177–46194; width and distance are metres, never squared.
        public static double Evaluate(double td,double proximity,double width,double side,double left,double right)
        {
            if(proximity>=8)return td;
            if(proximity<0){double t=(width+proximity)/width/2;return side>0?left*(.5+t)+right*(.5-t):right*(.5+t)+left*(.5-t);}
            double blend=proximity/8;return td*blend+(side>0?left:right)*(1-blend);
        }
    }
}
