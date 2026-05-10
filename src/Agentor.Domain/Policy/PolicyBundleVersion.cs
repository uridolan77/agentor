namespace Agentor.Domain.Policy;

public sealed class PolicyBundleVersion : IComparable<PolicyBundleVersion>, IEquatable<PolicyBundleVersion>
{
    public int Major { get; }
    public int Minor { get; }

    public static PolicyBundleVersion Initial => new(1, 0);

    public PolicyBundleVersion(int major, int minor)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(major, 1, nameof(major));
        ArgumentOutOfRangeException.ThrowIfNegative(minor, nameof(minor));
        Major = major;
        Minor = minor;
    }

    public static PolicyBundleVersion Parse(string value)
    {
        var parts = value?.Split('.') ?? [];
        if (parts.Length != 2
            || !int.TryParse(parts[0], System.Globalization.NumberStyles.None, null, out var major)
            || !int.TryParse(parts[1], System.Globalization.NumberStyles.None, null, out var minor)
            || major < 1
            || minor < 0)
        {
            throw new FormatException($"Invalid PolicyBundleVersion '{value}'. Expected 'major.minor' with major >= 1 (e.g. '1.0').");
        }

        return new PolicyBundleVersion(major, minor);
    }

    public static bool TryParse(string? value, out PolicyBundleVersion? result)
    {
        result = null;
        if (value is null)
        {
            return false;
        }
        var parts = value.Split('.');
        if (parts.Length != 2
            || !int.TryParse(parts[0], System.Globalization.NumberStyles.None, null, out var major)
            || !int.TryParse(parts[1], System.Globalization.NumberStyles.None, null, out var minor)
            || major < 1
            || minor < 0)
        {
            return false;
        }

        result = new PolicyBundleVersion(major, minor);
        return true;
    }

    public int CompareTo(PolicyBundleVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        var cmp = Major.CompareTo(other.Major);
        return cmp != 0 ? cmp : Minor.CompareTo(other.Minor);
    }

    public bool Equals(PolicyBundleVersion? other) =>
        other is not null && Major == other.Major && Minor == other.Minor;

    public override bool Equals(object? obj) => Equals(obj as PolicyBundleVersion);
    public override int GetHashCode() => HashCode.Combine(Major, Minor);
    public override string ToString() => $"{Major}.{Minor}";

    public static bool operator ==(PolicyBundleVersion? l, PolicyBundleVersion? r) => l?.Equals(r) ?? r is null;
    public static bool operator !=(PolicyBundleVersion? l, PolicyBundleVersion? r) => !(l == r);
    public static bool operator <(PolicyBundleVersion l, PolicyBundleVersion r) => l.CompareTo(r) < 0;
    public static bool operator >(PolicyBundleVersion l, PolicyBundleVersion r) => l.CompareTo(r) > 0;
    public static bool operator <=(PolicyBundleVersion l, PolicyBundleVersion r) => l.CompareTo(r) <= 0;
    public static bool operator >=(PolicyBundleVersion l, PolicyBundleVersion r) => l.CompareTo(r) >= 0;
}
