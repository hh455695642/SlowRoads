using System.Runtime.CompilerServices;

// Low-level synchronous windows are implementation details. Numerical evidence
// tests retain access; production terrain consumers use TerrainBuildContext.
[assembly: InternalsVisibleTo("SlowRoads.Generation.Tests")]
