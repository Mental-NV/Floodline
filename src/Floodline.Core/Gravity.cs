namespace Floodline.Core;

public enum GravityDirection
{
    Down,
    North,
    South,
    East,
    West
}

public static class GravityTable
{
    public static Int3 GetVector(GravityDirection dir) => dir switch
    {
        GravityDirection.Down => new(0, -1, 0),
        GravityDirection.North => new(0, 0, -1),
        GravityDirection.South => new(0, 0, 1),
        GravityDirection.East => new(1, 0, 0),
        GravityDirection.West => new(-1, 0, 0),
        _ => throw new ArgumentOutOfRangeException(nameof(dir))
    };

    public static Int3 GetUpVector(GravityDirection dir) => -GetVector(dir);

    public static Int3 GetRightVector(GravityDirection dir) => dir switch
    {
        GravityDirection.Down => new(1, 0, 0),
        GravityDirection.North => new(1, 0, 0),
        GravityDirection.South => new(1, 0, 0),
        GravityDirection.East => new(0, 0, 1),
        GravityDirection.West => new(0, 0, 1),
        _ => throw new ArgumentOutOfRangeException(nameof(dir))
    };

    public static Int3 GetForwardVector(GravityDirection dir)
    {
        Int3 u = GetUpVector(dir);
        Int3 r = GetRightVector(dir);
        return u.Cross(r);
    }
}

public readonly record struct TieCoord(int U, int R, int F) : IComparable<TieCoord>
{
    public int CompareTo(TieCoord other)
    {
        int uComp = U.CompareTo(other.U);
        if (uComp != 0)
        {
            return uComp;
        }

        int rComp = R.CompareTo(other.R);
        return rComp != 0 ? rComp : F.CompareTo(other.F);
    }

    public static bool operator <(TieCoord left, TieCoord right) => left.CompareTo(right) < 0;
    public static bool operator <=(TieCoord left, TieCoord right) => left.CompareTo(right) <= 0;
    public static bool operator >(TieCoord left, TieCoord right) => left.CompareTo(right) > 0;
    public static bool operator >=(TieCoord left, TieCoord right) => left.CompareTo(right) >= 0;
}

public static class DeterministicOrdering
{
    public static TieCoord GetTieCoord(Int3 c, GravityDirection dir)
    {
        Int3 u = GravityTable.GetUpVector(dir);
        Int3 r = GravityTable.GetRightVector(dir);
        Int3 f = GravityTable.GetForwardVector(dir);

        return new TieCoord(c.Dot(u), c.Dot(r), c.Dot(f));
    }

    public static int GetGravElev(Int3 c, GravityDirection dir) => c.Dot(GravityTable.GetUpVector(dir));

    public static Comparison<Int3> GetComparison(GravityDirection dir) => (a, b) =>
    {
        int elevComp = GetGravElev(a, dir).CompareTo(GetGravElev(b, dir));
        return elevComp != 0 ? elevComp : GetTieCoord(a, dir).CompareTo(GetTieCoord(b, dir));
    };
}
