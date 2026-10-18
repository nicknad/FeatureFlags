namespace NN.FeatureFlags;

public sealed record TargetingPolicy(
    IReadOnlySet<string>? TenantAllowList = null,
    IReadOnlySet<string>? UserAllowList = null,
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
        var hash = default(HashCode);
        hash.Add(RolloutPercentage);
        AddSet(ref hash, TenantAllowList);
        AddSet(ref hash, UserAllowList);
        return hash.ToHashCode();
    }

    private static bool SetEquals(IReadOnlySet<string>? left, IReadOnlySet<string>? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        return left is not null && right is not null && left.SetEquals(right);
    }

    private static void AddSet(ref HashCode hash, IReadOnlySet<string>? values)
    {
        if (values is null)
        {
            hash.Add(0);
            return;
        }

        hash.Add(values.Count);
        foreach (var value in values.Order(StringComparer.Ordinal))
        {
            hash.Add(value, StringComparer.Ordinal);
        }
    }
}
