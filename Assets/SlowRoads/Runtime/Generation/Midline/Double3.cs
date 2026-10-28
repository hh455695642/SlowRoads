using System;

namespace SlowRoads.Generation.Midline
{
    /// <summary>Double precision Vector3 subset used by Se (Dev L01111–L01299).</summary>
    public readonly struct Double3
    {
        public readonly double X, Y, Z;

        public Double3(double x, double y, double z) { X = x; Y = y; Z = z; }
        public double Length() => Math.Sqrt(X * X + Y * Y + Z * Z);
        public Double3 Multiply(double value) => new Double3(X * value, Y * value, Z * value);
        public Double3 Negate() => new Double3(-X, -Y, -Z);
        public Double3 Normalize()
        {
            // Three.Vector3.normalize uses 1 / (length || 1), including zero vectors.
            double length = Length();
            return Multiply(1 / (length == 0 ? 1 : length));
        }
        public double Dot(Double3 other) => X * other.X + Y * other.Y + Z * other.Z;
        public Double3 Lerp(Double3 other, double amount) => new Double3(
            X + (other.X - X) * amount,
            Y + (other.Y - Y) * amount,
            Z + (other.Z - Z) * amount);
        public Double3 WithY(double y) => new Double3(X, y, Z);
    }
}
