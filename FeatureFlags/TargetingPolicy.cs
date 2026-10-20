namespace NN.FeatureFlags;

/// <summary>
/// Controls who may see an enabled flag. Conditions are OR-ed: an allow-list match or a successful
/// rollout includes the subject.
/// </summary>
/// <param name="TenantAllowList">Tenants that are always included.</param>
/// <param name="UserAllowList">Users that are always included.</param>
/// <param name="RolloutPercentage">
/// The percentage (0-100) of targeting keys included, or <see langword="null"/> to disable rollout.
/// </param>
public sealed record TargetingPolicy(
    IReadOnlyCollection<string>? TenantAllowList = null,
    IReadOnlyCollection<string>? UserAllowList = null,
    int? RolloutPercentage = null)
{
    /// <summary>
    /// Creates a policy that allows the specified tenants and no one else.
    /// </summary>
    /// <param name="tenantIds">The tenant identifiers to include.</param>
    /// <returns>A tenant allow-list policy.</returns>
    public static TargetingPolicy ForTenants(params string[] tenantIds)
    {
        Guard.NotNull(tenantIds, nameof(tenantIds));

        return new TargetingPolicy(TenantAllowList: tenantIds);
    }

    /// <summary>
    /// Creates a policy that allows the specified users and no one else.
    /// </summary>
    /// <param name="userIds">The user identifiers to include.</param>
    /// <returns>A user allow-list policy.</returns>
    public static TargetingPolicy ForUsers(params string[] userIds)
    {
        Guard.NotNull(userIds, nameof(userIds));

        return new TargetingPolicy(UserAllowList: userIds);
    }

    /// <summary>
    /// Creates a percentage-rollout policy.
    /// </summary>
    /// <param name="percentage">The percentage of targeting keys to include, from 0 to 100.</param>
    /// <returns>A percentage-rollout policy.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The percentage is outside 0-100.</exception>
    public static TargetingPolicy Percentage(int percentage)
    {
        if (percentage is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(percentage),
                percentage,
                "Rollout percentage must be between 0 and 100.");
        }

        return new TargetingPolicy(RolloutPercentage: percentage);
    }

    /// <inheritdoc/>
    public bool Equals(TargetingPolicy? other)
    {
        return other is not null
            && RolloutPercentage == other.RolloutPercentage
            && SetEquals(TenantAllowList, other.TenantAllowList)
            && SetEquals(UserAllowList, other.UserAllowList);
    }

    /// <inheritdoc/>
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
