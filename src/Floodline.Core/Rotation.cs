namespace Floodline.Core;

/// <summary>
/// Defines the local axes for piece rotation.
/// </summary>
public enum RotationAxis
{
    /// <summary>
    /// Around the Y-axis (Standard 2D rotation).
    /// </summary>
    Yaw,

    /// <summary>
    /// Around the X-axis.
    /// </summary>
    Pitch,

    /// <summary>
    /// Around the Z-axis.
    /// </summary>
    Roll
}

public readonly record struct Matrix3x3(
    int M11, int M12, int M13,
    int M21, int M22, int M23,
    int M31, int M32, int M33)
{
    public Int3 Transform(Int3 v) => new(
        (M11 * v.X) + (M12 * v.Y) + (M13 * v.Z),
        (M21 * v.X) + (M22 * v.Y) + (M23 * v.Z),
        (M31 * v.X) + (M32 * v.Y) + (M33 * v.Z)
    );

    public static readonly IReadOnlyList<Matrix3x3> AllRotations = GenerateRotations();

    // Canonical 90-degree rotations
    public static readonly Matrix3x3 Identity = new(1, 0, 0, 0, 1, 0, 0, 0, 1);

    // Yaw (Y-axis)
    public static readonly Matrix3x3 YawCW = new(0, 0, 1, 0, 1, 0, -1, 0, 0); // Rotates X to -Z
    public static readonly Matrix3x3 YawCCW = new(0, 0, -1, 0, 1, 0, 1, 0, 0); // Rotates X to Z

    // Pitch (X-axis)
    public static readonly Matrix3x3 PitchCW = new(1, 0, 0, 0, 0, -1, 0, 1, 0); // Rotates Y to Z
    public static readonly Matrix3x3 PitchCCW = new(1, 0, 0, 0, 0, 1, 0, -1, 0); // Rotates Y to -Z

    // Roll (Z-axis)
    public static readonly Matrix3x3 RollCW = new(0, -1, 0, 1, 0, 0, 0, 0, 1); // Rotates X to Y
    public static readonly Matrix3x3 RollCCW = new(0, 1, 0, -1, 0, 0, 0, 0, 1); // Rotates X to -Y

    private static List<Matrix3x3> GenerateRotations()
    {
        List<Matrix3x3> rotations = [];
        int[] vals = [-1, 0, 1];

        foreach (int m11 in vals)
        {
            foreach (int m12 in vals)
            {
                foreach (int m13 in vals)
                {
                    foreach (int m21 in vals)
                    {
                        foreach (int m22 in vals)
                        {
                            foreach (int m23 in vals)
                            {
                                foreach (int m31 in vals)
                                {
                                    foreach (int m32 in vals)
                                    {
                                        foreach (int m33 in vals)
                                        {
                                            Matrix3x3 m = new(m11, m12, m13, m21, m22, m23, m31, m32, m33);
                                            if (m.IsRotationMatrix())
                                            {
                                                rotations.Add(m);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        return rotations;
    }

    private bool IsRotationMatrix()
    {
        // Determinant must be 1
        int det = (M11 * ((M22 * M33) - (M23 * M32))) -
                  (M12 * ((M21 * M33) - (M23 * M31))) +
                  (M13 * ((M21 * M32) - (M22 * M31)));

        if (det != 1)
        {
            return false;
        }

        // Must be orthogonal: M * M^T = I
        return IsOrthogonal();
    }

    private bool IsOrthogonal()
    {
        // Simplified check for integer matrices with elements in {-1, 0, 1}
        // Each row/column must have exactly one non-zero element
        int row1 = (M11 * M11) + (M12 * M12) + (M13 * M13);
        int row2 = (M21 * M21) + (M22 * M22) + (M23 * M23);
        int row3 = (M31 * M31) + (M32 * M32) + (M33 * M33);

        if (row1 != 1 || row2 != 1 || row3 != 1)
        {
            return false;
        }

        // Dot products between rows must be 0
        int dot12 = (M11 * M21) + (M12 * M22) + (M13 * M23);
        int dot13 = (M11 * M31) + (M12 * M32) + (M13 * M33);
        int dot23 = (M21 * M31) + (M22 * M32) + (M23 * M33);

        return dot12 == 0 && dot13 == 0 && dot23 == 0;
    }
}

public static class OrientationGenerator
{
    public static (IReadOnlyList<IReadOnlyList<Int3>> Rotated, IReadOnlyList<IReadOnlyList<Int3>> Normalized) GetUniqueOrientations(IReadOnlyList<Int3> voxels)
    {
        List<List<Int3>> rotatedList = [];
        List<List<Int3>> normalizedList = [];
        HashSet<string> seenHashes = [];

        foreach (Matrix3x3 matrix in Matrix3x3.AllRotations)
        {
            List<Int3> rotated = [.. voxels.Select(matrix.Transform)];
            List<Int3> normalized = Normalize(rotated);
            string hash = GetHash(normalized);

            if (!seenHashes.Contains(hash))
            {
                _ = seenHashes.Add(hash);
                rotatedList.Add(rotated);
                normalizedList.Add(normalized);
            }
        }

        return (
            [.. rotatedList.Select(l => (IReadOnlyList<Int3>)l.AsReadOnly())],
            [.. normalizedList.Select(l => (IReadOnlyList<Int3>)l.AsReadOnly())]
        );
    }

    public static List<Int3> Normalize(List<Int3> voxels)
    {
        int minX = voxels.Min(v => v.X);
        int minY = voxels.Min(v => v.Y);
        int minZ = voxels.Min(v => v.Z);

        return [.. voxels.Select(v => new Int3(v.X - minX, v.Y - minY, v.Z - minZ))
                     .OrderBy(v => v.X)
                     .ThenBy(v => v.Y)
                     .ThenBy(v => v.Z)];
    }

    private static string GetHash(List<Int3> voxels) => string.Join(";", voxels.Select(v => $"{v.X},{v.Y},{v.Z}"));
}
