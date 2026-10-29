using SlowRoads.Generation.Heightfield;

namespace SlowRoads.Generation.Midline
{
    /// <summary>
    /// Effective Dev P + Hills style configuration. Dev L00821–L00858,
    /// Hills L01561–L01596. Values remain JS Number-compatible doubles.
    /// </summary>
    public sealed class MidlineConfig
    {
        public enum HillsStyle { Straight = 0, Hills = 3 }

        public HillsStyle Style { get; }
        public string LocalStartSeed { get; } = "1"; // d.seed is numeric 1; JS 1 + "" + generationCount.
        public bool IsStraight { get; }
        public bool Spiralise { get; }
        public bool OneDir { get; }
        public Double3? StartPosition { get; }
        public Double3? StartHeading { get; }
        public Double3? Direction { get; }
        public int SmoothWindow { get; }
        public double GenRadius { get; }
        public double GenMinRadius { get; }
        public double RestartMargin { get; } = 200;
        public double MinStartElevation { get; } = 10;
        public double MaxStartElevation { get; }
        public double RoadSink { get; }
        public double MaxGrad { get; }
        public double MaxTurnDelta { get; }
        public double SmoothFactor { get; }
        public double CurvatureHeightFactor { get; }
        public int MinFineSegments { get; }
        public int FineHorizon { get; }
        // Hills xl supplies the road-edge sampling offset to B7; not a steering parameter.
        public double RoadHalfWidth {get;}
        public double SolidLineThreshold { get; }
        public double SolidLineCrestThreshold { get; }
        public double MarkingsMargin { get; }
        public double FeelDistance { get; } = 10;
        public double FeelAngle { get; } = 0.463;
        public double NodeSpacing { get; } = 10;
        public double MinimumElevation { get; } = 2;
        public double MaximumTargetOverflow { get; } = 3;
        public double MaximumLongitudinalGrade { get; } = 5;
        public double MinimumBridgeElevation { get; } = 0;
        public double MaximumBridgeElevation { get; } = 4;
        public int MaximumBridgeNodes { get; } = 35;
        public int MinimumBridgeNodes { get; } = 3;
        public bool TestBridgeLateral { get; } = true;
        public double SmoothingStrength { get; } = 1;
        public double GenDeviation { get; } = 0;

        MidlineConfig(HillsStyle style, Double3? startPosition, Double3? startHeading,
            Double3? direction, bool oneDir,int fineHorizon=400,double roadHalfWidth=3.7)
        {
            Style = style;
            StartPosition = startPosition;
            StartHeading = startHeading;
            Direction = direction;
            OneDir = oneDir;
            if(fineHorizon<1)throw new System.ArgumentOutOfRangeException(nameof(fineHorizon));
            FineHorizon=fineHorizon;
            if(!(roadHalfWidth>0)||double.IsInfinity(roadHalfWidth))throw new System.ArgumentOutOfRangeException(nameof(roadHalfWidth));
            RoadHalfWidth=roadHalfWidth;
            IsStraight = style == HillsStyle.Straight;
            Spiralise = !direction.HasValue && !IsStraight;
            SmoothWindow = IsStraight ? 9 : 7;
            GenRadius = IsStraight ? 100 : 12000;
            GenMinRadius = IsStraight ? 100 : 6000;
            MaxStartElevation = IsStraight ? 1000 : 180000;
            RoadSink = IsStraight ? 0.2 : 0.4;
            MaxGrad = IsStraight ? 0.2 : 0.12;
            MaxTurnDelta = IsStraight ? 0.7 : 0.2;
            SmoothFactor = IsStraight ? 1 : 0.75;
            CurvatureHeightFactor = IsStraight ? 50 : 3;
            MinFineSegments = IsStraight ? 4 : 1;
            SolidLineThreshold = IsStraight ? 0.1 : 0.18;
            SolidLineCrestThreshold = IsStraight ? -0.02 : -0.5;
            MarkingsMargin = IsStraight ? 12 : 5;
        }

        public static MidlineConfig ForHills(HillsStyle style) => new MidlineConfig(style, null, null, null, false);

        // Test and later configuration extension point. A supplied start Y is intentionally
        // ignored by Se.getNextStartingPose; the source samples it from the heightmap.
        public MidlineConfig WithStart(Double3? position, Double3? heading) =>
            new MidlineConfig(Style, position, heading, Direction, OneDir,FineHorizon,RoadHalfWidth);
        public MidlineConfig WithFineHorizon(int value)=>
            new MidlineConfig(Style,StartPosition,StartHeading,Direction,OneDir,value,RoadHalfWidth);
        public MidlineConfig WithOneDir(bool value)=>
            new MidlineConfig(Style,StartPosition,StartHeading,Direction,value,FineHorizon,RoadHalfWidth);
        public MidlineConfig WithRoadHalfWidth(double value)=>
            new MidlineConfig(Style,StartPosition,StartHeading,Direction,OneDir,FineHorizon,value);

        public HillsHeightfieldConfig CreateHeightfieldConfig(string seed)
        {
            if (Style == HillsStyle.Straight)
                return new HillsHeightfieldConfig(seed, scale: 80, offset: 90,
                    layerResolutions: new[] {3, 21, 17, 31}, depthHeightFactor: 0.7,
                    squared: true, temper: true, temperBase: 50, temperBelow: 400,
                    temperMin: 0, spiralise: false, maxCached: 8, tileScaleRandom: 0.3);
            return new HillsHeightfieldConfig(seed, scale: 60, offset: 100,
                layerResolutions: new[] {3, 9, 13, 27, 31}, depthHeightFactor: 1.25,
                squared: true, temper: true, temperBase: 50, temperBelow: 500,
                temperMin: 0.5, spiralise: true, maxCached: 8, tileScaleRandom: 0.25);
        }
    }
}
