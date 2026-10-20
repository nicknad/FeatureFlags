namespace NN.FeatureFlags;

public sealed record TargetingPolicy(
    IReadOnlyCollection<string>? TenantAllowList = null,
    IReadOnlyCollection<string>? UserAllowList = null,
    int? RolloutPercentage = null)
{
    public bool Equals(TargetingPolicy? other)
    {
        return other is not null
            && RolloutPercentage == other.RolloutPercentage
            && SetEquals(TenantAllowList, other.TenantAllowList)
            && SetEquals(UserAllowList, other.UserAllowList);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = RolloutPercentage.GetHashCode();
            hash = (hash * 397) ^ GetSetHashCode(TenantAllowList);
            hash = (hash * 397) ^ GetSetHashCode(UserAllowList);
            return hash;
        }
    }

    private static bool SetEquals(IReadOnlyCollection<string>? left, IReadOnlyCollection<string>? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null || left.Count != right.Count)
        {
            return false;
        }

        return new HashSet<string>(left, StringComparer.Ordinal).SetEquals(right);
    }

    private static int GetSetHashCode(IReadOnlyCollection<string>? values)
    {
        if (values is null)
        {
            return 0;
        }

        var hash = values.Count;
        foreach (var value in values.OrderBy(static value => value, StringComparer.Ordinal))
        {
            hash = unchecked((hash * 397) ^ StringComparer.Ordinal.GetHashCode(value));
        }

        return hash;
    }
}
